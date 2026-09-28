using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION archetype): the world's answer to an enemy's bite, drawn on its
/// own layer and never on the champion's figure. JAWS is the reference and it is a FRONTAL SHADOW BITE (the owner,
/// 2026-09-28, pointing at Roni Kangaskorte's "Bite VFX" on ArtStation: "not a bad jaw biting from the side: the earlier
/// perspective, biting from the FRONT, but a more effective and beautiful jaw"). When the fight resolves the reflected
/// blow, a crown of Shadow fangs (two long canines at its corners, bowing out and hooking in, five packed leaf-shaped
/// teeth between them) and a lower row condense out of mist, wide apart, framing the head of the creature that bit; they
/// CHARGE (a pale lavender-grey heating to violet and magenta), part further (the wind-up), then SLAM together across
/// it, the rows interlocking white-hot for one frame; on that snap the teeth break into the IMPACT and the energy cools
/// back through magenta and violet to dark Shadow. ONE SHADOW PHENOMENON (the owner's polish brief, 2026-09-28), never
/// separate effects in a row: SHADOW MIST GATHERS, the FANGS CONDENSE FROM IT, THE SHADOW COMPRESSES, SNAP, THE SHADOW
/// BURSTS OUTWARD, THE TEETH BREAK BACK INTO THE MIST, EVERYTHING EVAPORATES. The fangs stay the primary shape;
/// the hot core the punctuation; the pressure ring secondary; the splinters and the mist tertiary; the speed lines an
/// accent only (the diagonal slash was removed: it read as a blade). While the champion performs an action, JAWS stays
/// quieter than it.
/// ENEMY HITS SEEKER, SHADOW FANGS BITE IT, "−X JAWS", THE BITE BURSTS AND EVAPORATES.
/// </summary>
/// <remarks>
/// <para>
/// A Reaction takes no beat, so it owns no clip and is never an <see cref="IActionPerformance"/> (that interface is the
/// figure's owner). Everything is measured from the first frame that shows the enemy's contact (the bite's millisecond,
/// on which Core also resolves the reaction and its answer). On screen the answer is SCHEDULED: the cue and a kill's
/// fall land on the SNAP (the rows meet); the number a few frames after it, so the eye sees the bite before it reads the
/// number.
/// </para>
/// <para>
/// SEVEN PARTS, composed at runtime (tools/asset-pipeline/v2/seeker_bite.py; ADR-010's rule): the two rows of fangs
/// (each in <see cref="FangStates"/> condensation states, mist to crisp), the Shadow MIST lobe the phrase is made of, and
/// the impact's flash, ring (crisp and dissolving), speed lines and splinters (crisp and softened). All are white or grey
/// and TINTED here along the Shadow palette. The mist is drawn BEFORE the creatures (atmosphere behind the bitten one);
/// the teeth, their glow, the dissolving ring and the splinters after the creatures and BEFORE the champion (the glow
/// adds its light inside that batch, with a zero alpha); the flash, the crisp ring and the speed lines are light (the
/// shared additive pass). NO JAWS TARGET FLASH: the bitten creature's own sprite
/// never flashes (the generic F2 for ordinary blows is untouched).
/// </para>
/// <para>
/// WHAT IS GONE FOR GOOD: the spring-loaded bear trap, its chain, its housing, the tether, the yank, the reel-in, the
/// bite glyphs on the champion, the mirrored snap, the swarm, the whole piranha, the teeth that stopped on the creature's
/// outline without ever meeting, the four misty triangles, and the side-view maw (a jaw that bit from the side). Their
/// art stays on disk as history and is never played. The champion does nothing; SPRAY, HARD HANDS and the basic swing
/// are never interrupted. REPAY keeps its own legacy presentation (the lookup is by SKILL id). The dock's REARMING /
/// READY state is Core's <c>ReactionArmed</c> report.
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART: two rows of fangs and the impact's five parts (tools/asset-pipeline/v2/seeker_bite.py) ──

    /// <summary>
    /// The UPPER CROWN of fangs, seen from the front: <see cref="FangStates"/> states side by side, each cell
    /// <see cref="UpperCell"/>, from a loose drift of mist (0) to the crisp glowing teeth (last). Two long canines at its
    /// corners that bow out and hook their points in, five packed leaf-shaped teeth between them (each overlapping the
    /// next) hanging from an arched gum, their points on one flat line.
    /// </summary>
    public string UpperKey { get; init; } = "fxp_seeker_bite_upper";

    /// <summary>The LOWER ROW: four leaf-shaped teeth whose points rise between the upper points, and a taller one at each end that the canines close outside of, in the same states.</summary>
    public string LowerKey { get; init; } = "fxp_seeker_bite_lower";

    /// <inheritdoc cref="UpperKey"/>
    public int FangStates { get; init; } = 6;

    /// <summary>
    /// The SNAP cell, after the <see cref="FangStates"/> condensation states: the teeth are SMOKE held in a tooth's shape
    /// (the owner, 2026-09-28: "more like smoke, more like mist"), and on the snap frame only the smoke condenses HARD
    /// into solid teeth, white-hot (the one frame the jaw is visibly shut: drawn as smoke, it was a star over a haze and
    /// the bite never closed), then releases back into smoke as it breaks.
    /// </summary>
    public int SnapCell => FangStates;

    /// <inheritdoc cref="UpperKey"/>
    public Point UpperCell { get; init; } = new(256, 200);

    /// <inheritdoc cref="LowerKey"/>
    public Point LowerCell { get; init; } = new(224, 120);

    /// <summary>In the upper cell (texture px): the crown's centre line and the deepest point of its INNER teeth, its bite line (the canines reach lower, outside the lower row).</summary>
    public Vector2 UpperBitePoint { get; init; } = new(128f, 116f);

    /// <summary>In the lower cell (texture px): the row's centre line and its middle points' line, its bite line (the taller end teeth rise past it, outside the upper inner teeth).</summary>
    public Vector2 LowerBitePoint { get; init; } = new(112f, 40f);

    /// <summary>The upper crown's width in the art (texture px), canine to canine: the size reference.</summary>
    public float CrownArtWidth { get; init; } = 209.9f;

    /// <summary>The impact's parts: a small hot core with six fat rays; the pressure ring (crisp and softened); a dozen radial speed lines; torn splinters of the broken teeth (crisp and softened).</summary>
    public string FlashKey { get; init; } = "fxp_seeker_bite_star";

    /// <inheritdoc cref="FlashKey"/>
    public string RingKey { get; init; } = "fxp_seeker_bite_ring";

    /// <inheritdoc cref="FlashKey"/>
    public string StreaksKey { get; init; } = "fxp_seeker_bite_streaks";

    /// <inheritdoc cref="FlashKey"/>
    public string ShardsKey { get; init; } = "fxp_seeker_bite_shards";

    /// <summary>The Shadow MIST: one soft irregular smoke lobe, drawn as <see cref="MistLobes"/> turned apart into one volume.</summary>
    public string MistKey { get; init; } = "fxp_seeker_bite_smoke";

    // ── SIZE AND PLACE: the bite frames the creature that bit, from the front ─────────────────────

    /// <summary>
    /// The crown's width as a share of the creature's CANONICAL body width (not its drawn silhouette: a lunge is drawn on
    /// a canvas far wider than the creature), clamped. Every other part is sized off the crown.
    /// <see cref="ReactionRecipes.SizeDial"/> scales it for a film.
    /// </summary>
    public float CrownWidthShare { get; init; } = 0.45f;

    /// <inheritdoc cref="CrownWidthShare"/>
    public float CrownMinPx { get; init; } = 80f;

    /// <inheritdoc cref="CrownWidthShare"/>
    public float CrownMaxPx { get; init; } = 240f;

    /// <summary>
    /// The bite's centre line, a share of the canonical body's width from its left: toward the creature's FRONT (every
    /// creature faces the Seeker, left), where its head is. Centred on the whole body, the bite fell between two heads of a
    /// pack (a tail widens the body behind the head) and a canine sat on the next creature's eyes.
    /// </summary>
    public float CentreShare { get; init; } = 0.30f;

    /// <summary>
    /// Where the rows MEET, a share of the creature's drawn height from its top (read off the silhouette pinned from the
    /// pose it bit in): about its middle, so the open rows frame the creature above and below and the bite lands on it.
    /// </summary>
    public float MeetShare { get; init; } = 0.52f;

    /// <summary>
    /// How far apart the rows' bite lines stand when open, a share of the creature's drawn height: wide enough to frame
    /// it (the crown below its health bar, the lower row above the skill dock)...
    /// </summary>
    public float OpenGapShare { get; init; } = 0.62f;

    /// <summary>...parting this much further through the wind-up (a share of the open gap, enough to be seen: at an eighth the rows looked still), the rear before the slam...</summary>
    public float WindUpShare { get; init; } = 0.25f;

    /// <summary>...and, shut, the points PASS each other by this much (art px, scaled with the crown): the rows interlock.</summary>
    public float InterlockArtPx { get; init; } = 26f;

    /// <summary>
    /// When one answer bites SEVERAL creatures, every bite but the FRONT one (the creature nearest the Seeker) is this
    /// much smaller, on its own creature's head, and its impact is only the flash and the splinters (two bites, not one
    /// wide pop); the one cue is the front bite's. They snap together: the fight answered both on the same bite, and a
    /// later second bite let its creature fall and its number show before its own rows had shut.
    /// </summary>
    public float SecondaryScale { get; init; } = 0.68f;

    /// <summary>
    /// The impact's parts, their sizes as multiples of the crown's width: the payoff is bigger than the jaw that causes it
    /// (a ring smaller than the crown made the bite's end weaker than its build-up).
    /// </summary>
    public float FlashSize { get; init; } = 1.3f;

    /// <inheritdoc cref="FlashSize"/>
    public float RingSize { get; init; } = 1.8f;

    /// <inheritdoc cref="FlashSize"/>
    public float StreaksSize { get; init; } = 2.8f;

    /// <inheritdoc cref="FlashSize"/>
    public float ShardsSize { get; init; } = 1.5f;

    // ── THE SHADOW MIST: one material volume behind the fangs, with ONE lifecycle (fade in, condense, compress, snap,
    // expand, fade out; never a second fade-in) whose pulse is its form and density, not a flashing opacity ──

    /// <summary>The mist's size at scale 1, as multiples of the crown's width: an upright oval behind both rows, wide enough to show AROUND the bitten creature (drawn behind it, a mist the size of the jaw was hidden by the body).</summary>
    public Vector2 MistSize { get; init; } = new(2.9f, 2.7f);

    /// <summary>
    /// The lobes of the one volume (three soft irregular sprites; no emitter): each an offset from the bite (in crown
    /// widths), a FLIP of the one texture (0 none, 1 horizontal, 2 vertical, 3 both: a tilt put the oval's height and its
    /// gap-driven stretch on a slanted axis) and a scale. The FIRST is the outer edge, drawn in <see cref="MistEdgeColor"/>
    /// a little larger; the others are the dark core.
    /// </summary>
    public IReadOnlyList<Vector4> MistLobes { get; init; } = new[]
    {
        new Vector4(0f, 0f, 3f, 1.12f), new Vector4(0f, 0f, 0f, 1f), new Vector4(0.12f, 0.06f, 1f, 0.78f),
    };

    /// <summary>
    /// The mist's opacity as the WHOLE stack reads (each lobe is drawn at the share that stacks to it; per-lobe values
    /// stacked to half as dense again): a whisper at the spawn, translucent when the fangs have formed, densest at the
    /// snap. The mist is drawn BEHIND the creatures (atmosphere around the bitten creature, never a veil over it: drawn over
    /// it, it greyed the black body and dimmed its eyes); the edge lobe at <see cref="MistEdgeShare"/> of the core.
    /// </summary>
    public float MistAlphaAtSpawn { get; init; } = 0.07f;

    /// <inheritdoc cref="MistAlphaAtSpawn"/>
    public float MistAlphaFormed { get; init; } = 0.38f;

    /// <inheritdoc cref="MistAlphaAtSpawn"/>
    public float MistAlphaAtSnap { get; init; } = 0.45f;

    /// <inheritdoc cref="MistAlphaAtSpawn"/>
    public float MistEdgeShare { get; init; } = 0.8f;

    /// <summary>
    /// The mist's scale: larger as it gathers, whole when the fangs have formed, COMPRESSED around the bite at the snap
    /// (the charge's tension is its form, not a pulse of light), then released slowly OUTWARD through the tail (a fast bite,
    /// a slow atmospheric release).
    /// </summary>
    public float MistScaleAtSpawn { get; init; } = 1.12f;

    /// <inheritdoc cref="MistScaleAtSpawn"/>
    public float MistScaleAtSnap { get; init; } = 0.92f;

    /// <inheritdoc cref="MistScaleAtSpawn"/>
    public float MistScaleTail { get; init; } = 1.22f;

    /// <summary>The lobes draw together by this share of their offsets as the mist compresses (a drift inward of a few px).</summary>
    public float MistConverge { get; init; } = 0.35f;

    /// <summary>
    /// The FANG GEOMETRY controls the mist's height: it stretches with the rows' gap by this share of the gap's own change
    /// (taller through the wind-up, squeezed as they slam), within these bounds.
    /// </summary>
    public float MistStretch { get; init; } = 0.35f;

    /// <inheritdoc cref="MistStretch"/>
    public float MistStretchMin { get; init; } = 0.88f;

    /// <inheritdoc cref="MistStretch"/>
    public float MistStretchMax { get; init; } = 1.09f;

    /// <summary>The speed lines' peak opacity: an accent of force, gone near the impact's peak, well before the splinters and the mist.</summary>
    public float StreaksPeakAlpha { get; init; } = 0.8f;

    /// <summary>
    /// While the champion is PERFORMING an action (SPRAY's throw, HARD HANDS' leap and punch), JAWS is never brighter or
    /// larger than it: the snap stays magenta-hot (no white), the flash is this much dimmer and smaller, and the speed lines
    /// are left out (a reduction only; measured at 2-3 times the punch's own contact flash, it took the punch's payoff).
    /// A second creature's bite is always this quiet.
    /// </summary>
    public float QuietFlashAlpha { get; init; } = 0.55f;

    /// <inheritdoc cref="QuietFlashAlpha"/>
    public float QuietFlashSize { get; init; } = 0.8f;

    // ── TIMING (ms after the first frame; the owner allowed it longer, so the bite has its build-up) ──

    /// <summary>
    /// APPEAR: the rows condense out of the mist over six frames (the strip's states, one a frame, loose to crisp; in one frame
    /// it read as a pop), growing out of nothing (<see cref="OpacityAtSpawn"/>) to <see cref="FormedOpacity"/> and
    /// tightening in (<see cref="ArriveScale"/>), already wide apart. The teeth reach full strength only as the charge
    /// completes, so the snap, not the fangs appearing, is the brightest moment.
    /// </summary>
    public float AppearMs { get; init; } = 100f;

    /// <inheritdoc cref="AppearMs"/>
    public float OpacityAtSpawn { get; init; } = 0.10f;

    /// <inheritdoc cref="AppearMs"/>
    public float FormedOpacity { get; init; } = 0.62f;

    /// <summary>
    /// The teeth's opacity on the snap, where the charge has brought them: less than whole (the owner, 2026-09-28: "more
    /// like smoke, more like mist, a little lower opacity"), so through the charge the smoke shows the creature through
    /// it; on the snap frame itself the solid <see cref="SnapCell"/> at this opacity, white-hot, with the glow, is still
    /// the brightest moment.
    /// </summary>
    public float SnapOpacity { get; init; } = 0.88f;

    /// <inheritdoc cref="AppearMs"/>
    public float ArriveScale { get; init; } = 1.12f;

    /// <summary>
    /// CHARGE: from the appear to the snap the teeth heat along <see cref="ChargeRamp"/> (pale lavender, violet,
    /// magenta), slowly at first (this power), so the eye sees the energy build. The WIND-UP (the rows parting a little
    /// further) runs from here to the close.
    /// </summary>
    public float ChargeEasePower { get; init; } = 1.3f;

    /// <inheritdoc cref="ChargeEasePower"/>
    public float WindUpFromMs { get; init; } = 150f;

    /// <summary>THE CLOSE: from here the rows slam together, accelerating (an ease-in of <see cref="CloseEasePower"/>: each 60 fps step larger than the last, the LARGEST on the snap frame itself).</summary>
    public float CloseFromMs { get; init; } = 233f;

    /// <summary>
    /// THE SNAP: the rows meet here, interlocked, on a frame (19 × 16.67 ms, set a hair under it so the playhead's thirds
    /// of a millisecond land ON the shut pose). The cue's transient and a kill's fall land on this frame; the impact starts.
    /// </summary>
    public float SnapAtMs { get; init; } = 316.6f;

    /// <inheritdoc cref="CloseFromMs"/>
    public float CloseEasePower { get; init; } = 2.6f;

    /// <summary>The shut teeth, white-hot on the snap frame, break into the impact over this long (a frame at half, then gone).</summary>
    public float TeethBreakMs { get; init; } = 33f;

    /// <summary>
    /// THE IMPACT, from the snap, in its hierarchy: the hot core, the FLASH, swells in one frame, holds whole two frames,
    /// then shrinks to a point and fades, cooling from white to magenta and violet, handing the scene back to the Shadow
    /// (<see cref="FlashMs"/>); the RING, the pressure escaping, grows out past the crown crisp and magenta, cooling to
    /// violet, while a softened copy takes over its edge and dissolves into the mist (<see cref="RingMs"/>); the SPLINTERS
    /// fly out with it from a frame after the snap, pale magenta and solid, darkening to violet and softening into Shadow
    /// fragments (<see cref="ShardsMs"/>); the MIST releases outward and evaporates (<see cref="BurstMs"/>, the whole
    /// impact). ACCENT ONLY: the SPEED LINES, gone near the impact's peak (<see cref="StreaksMs"/>). (The diagonal slash was
    /// removed: a directional stroke in a radial burst read as a blade, and JAWS is a bite.)
    /// </summary>
    public float FlashMs { get; init; } = 140f;

    /// <inheritdoc cref="FlashMs"/>
    public float RingMs { get; init; } = 300f;

    /// <summary>The crisp ring is whole for its first frames and gone by here; the softened ring carries the rest of <see cref="RingMs"/>.</summary>
    public float RingCrispMs { get; init; } = 200f;

    /// <summary>The softened ring's peak opacity (in the mist's edge colour).</summary>
    public float RingSoftPeakAlpha { get; init; } = 0.35f;

    /// <inheritdoc cref="FlashMs"/>
    public float StreaksMs { get; init; } = 70f;

    /// <inheritdoc cref="FlashMs"/>
    public float ShardsMs { get; init; } = 360f;

    /// <inheritdoc cref="FlashMs"/>
    public float BurstMs { get; init; } = 360f;

    /// <summary>The reaction is gone: the snap plus the impact.</summary>
    public float GoneMs => SnapAtMs + BurstMs;

    /// <summary>Below this opacity a part is not drawn at all.</summary>
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

    // ── THE SHADOW PALETTE (every part is white or grey in the art; these tint it) ────────────────

    /// <summary>The teeth's charge, cold to hot: a pale lavender-grey, lavender, violet, magenta (the reference's white to orange, in Shadow; the white is saved for the snap).</summary>
    public IReadOnlyList<Color> ChargeRamp { get; init; } = new[]
    {
        new Color(207, 196, 230), new Color(190, 156, 250), new Color(160, 96, 255), new Color(214, 84, 236),
    };

    /// <summary>
    /// On the snap frame, and that frame ONLY, the hot teeth flare this far toward <see cref="FlashColor"/> (white-hot): an
    /// impact punctuation, never the effect's new colour; from the next frame the energy cools.
    /// </summary>
    public float SnapFlare { get; init; } = 0.6f;

    /// <summary>
    /// The teeth's glow, drawn in the material batch with a zero alpha (it only adds light), a share of their opacity; it
    /// builds with the charge from <see cref="GlowAtFormed"/> of it (whole on the snap), so the formed teeth stay a pale
    /// lavender-grey and the snap is the brightest moment.
    /// </summary>
    public float GlowShare { get; init; } = 0.45f;

    /// <inheritdoc cref="GlowShare"/>
    public float GlowAtFormed { get; init; } = 0.2f;

    /// <summary>
    /// The impact's colours, cooling after the snap (never on into brighter pink): the flash white-hot on the snap frame,
    /// then magenta, then violet; the ring magenta cooling to violet; the splinters pale magenta darkening to Shadow violet.
    /// </summary>
    public Color FlashColor { get; init; } = new(255, 236, 255);

    /// <inheritdoc cref="FlashColor"/>
    public Color CoolColor { get; init; } = new(132, 78, 210);


    /// <inheritdoc cref="FlashColor"/>
    public Color RingColor { get; init; } = new(214, 84, 236);

    /// <inheritdoc cref="FlashColor"/>
    public Color StreakColor { get; init; } = new(236, 228, 255);

    /// <inheritdoc cref="FlashColor"/>
    public Color ShardColor { get; init; } = new(232, 150, 246);

    /// <inheritdoc cref="FlashColor"/>
    public Color ShardCoolColor { get; init; } = new(84, 48, 128);

    /// <summary>The mist: a near-black violet core with only a subtle violet edge (never bright additive purple smoke). The core is darker than the dungeon floor by a clear margin: a "dark" violet as light as the floor added back what the mist took away, and the volume did not read at all.</summary>
    public Color MistCoreColor { get; init; } = new(12, 7, 20);

    /// <inheritdoc cref="MistCoreColor"/>
    public Color MistEdgeColor { get; init; } = new(66, 40, 100);

    // ── SOUND, FLASH AND THE SCREEN'S GENERIC CUES ───────────────────────────────────────────────

    /// <summary>
    /// The reaction's ONE cue: a REAL BITE built from recorded foley (tools/asset-pipeline/make_jaws_bite.py, CC0
    /// sources in tools/asset-pipeline/foley/jaws_bite/), its onset exactly on the SNAP (<see cref="SnapAtMs"/>, the rows
    /// meet): real teeth and a real cabbage-and-flesh crunch tearing in chewing bursts, the weight landing ~150 ms later
    /// (a pitch-dropping sub and a real meat thud) with the crunch still grinding under it, wet squelches, cartilage ticks;
    /// ~580 ms, dense (the owner, 2026-09-28: "a real bite, like Trundle's Q in League of Legends"; the synthesised bites
    /// before it read as a thud). No metal, no roar, no whoosh. HUMAN-APPROVED (2026-09-28): JAWS is the gold-standard
    /// REACTION reference; the cue's bytes are pinned, so a change is a new approval.
    /// </summary>
    public IReadOnlyList<string> SnapCues { get; init; } = new[] { "sfx_seeker_jaws_bite", "sfx_hit" };

    /// <inheritdoc cref="SnapCues"/>
    public float SnapVolume { get; init; } = 0.42f;

    /// <summary>How wide the cue is panned toward its target (± at the arena's edges).</summary>
    public float PanWidth { get; init; } = 0.35f;

    /// <summary>
    /// The per-trigger callout ("SNARE"). Off: a reaction fires every few seconds, and the bite, the number ("−4 JAWS")
    /// and the dock already say what happened (RH_REACTION_CALLOUT=1 restores it, for a comparison film).
    /// </summary>
    public bool Callout { get; init; }

    /// <summary>
    /// The bitten creature's own flash for the reflected blow: NONE for JAWS (0). The bite and its impact are the
    /// reaction's hit feedback; a grey/white flash of the creature's sprite competed with them. The generic F2 for
    /// ordinary blows is untouched.
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

    /// <summary>RH_REACTION_SIZE=&lt;share&gt;: the bite's size against the recipe's (a size comparison film). 1 otherwise.</summary>
    public static readonly float SizeDial =
        float.TryParse(Environment.GetEnvironmentVariable("RH_REACTION_SIZE"), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0f ? s : 1f;

    /// <summary>THE SEEKER's JAWS (the REACTION reference): a frontal Shadow bite, bite for bite.</summary>
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
