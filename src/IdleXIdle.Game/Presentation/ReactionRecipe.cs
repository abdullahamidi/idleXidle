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
/// it, the rows interlocking white-hot; on that snap the teeth break into the IMPACT: a flash, a diagonal slash, a ring,
/// speed lines, splinters of the broken teeth and a Shadow haze, the splinters the last to fade.
/// ENEMY HITS SEEKER, SHADOW FANGS BITE IT, "−X JAWS", THE BITE BURSTS AND FADES.
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
/// EIGHT PARTS, composed at runtime (tools/asset-pipeline/v2/seeker_bite.py; ADR-010's rule): the two rows of fangs
/// (each in <see cref="FangStates"/> condensation states, mist to crisp), and the impact's flash, slash, ring, speed
/// lines, splinters and haze. All are white or grey and TINTED here along the Shadow palette. The teeth, their glow, the
/// splinters and the haze are drawn after the creatures and BEFORE the champion (the glow adds its light inside that
/// batch, with a zero alpha); the flash, the slash, the ring and the speed lines are light (the shared additive pass). NO JAWS TARGET FLASH: the bitten creature's own sprite
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

    /// <summary>The impact's parts: a small hot core with six fat rays; one tapered stroke (the slash); four tapered arcs; radial speed lines; torn splinters of the broken teeth; a ragged smoke disc.</summary>
    public string FlashKey { get; init; } = "fxp_seeker_bite_star";

    /// <inheritdoc cref="FlashKey"/>
    public string SlashKey { get; init; } = "fxp_seeker_bite_slash";

    /// <inheritdoc cref="FlashKey"/>
    public string RingKey { get; init; } = "fxp_seeker_bite_ring";

    /// <inheritdoc cref="FlashKey"/>
    public string StreaksKey { get; init; } = "fxp_seeker_bite_streaks";

    /// <inheritdoc cref="FlashKey"/>
    public string ShardsKey { get; init; } = "fxp_seeker_bite_shards";

    /// <inheritdoc cref="FlashKey"/>
    public string PuffKey { get; init; } = "fxp_seeker_bite_smoke";

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
    /// The impact's parts, their sizes as multiples of the crown's width (the slash's length): the payoff is bigger than
    /// the jaw that causes it (a ring smaller than the crown made the bite's end weaker than its build-up).
    /// </summary>
    public float FlashSize { get; init; } = 1.3f;

    /// <inheritdoc cref="FlashSize"/>
    public float SlashSize { get; init; } = 2.1f;

    /// <inheritdoc cref="FlashSize"/>
    public float RingSize { get; init; } = 1.8f;

    /// <inheritdoc cref="FlashSize"/>
    public float StreaksSize { get; init; } = 2.8f;

    /// <inheritdoc cref="FlashSize"/>
    public float ShardsSize { get; init; } = 1.5f;

    /// <inheritdoc cref="FlashSize"/>
    public float PuffSize { get; init; } = 1.2f;

    /// <summary>
    /// The slash's angle on screen (degrees, clockwise: positive falls to the right, as the reference's stroke does): a
    /// stroke with a direction, crossing the flash BETWEEN its rays and longer than it (laid along the star's own
    /// longest rays, the white star swallowed it).
    /// </summary>
    public float SlashAngleDegrees { get; init; } = 35f;

    // ── TIMING (ms after the first frame; the owner allowed it longer, so the bite has its build-up) ──

    /// <summary>
    /// APPEAR: the rows condense out of mist over six frames (the strip's states, one a frame, loose to crisp; in one frame
    /// it read as a pop), growing out of nothing (<see cref="OpacityAtSpawn"/>) to <see cref="FormedOpacity"/> and
    /// tightening in (<see cref="ArriveScale"/>), already wide apart. The teeth reach full strength only as the charge
    /// completes, so the snap, not the fangs appearing, is the brightest moment.
    /// </summary>
    public float AppearMs { get; init; } = 100f;

    /// <inheritdoc cref="AppearMs"/>
    public float OpacityAtSpawn { get; init; } = 0.10f;

    /// <inheritdoc cref="AppearMs"/>
    public float FormedOpacity { get; init; } = 0.8f;

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
    /// THE IMPACT, from the snap: the FLASH swells in one frame, holds whole two frames, then shrinks and fades
    /// (<see cref="FlashMs"/>); the SLASH strikes across it from the frame after the snap and fades (<see cref="SlashMs"/>);
    /// the RING grows out past the crown and fades (<see cref="RingMs"/>); the SPEED LINES burst out and fade
    /// (<see cref="StreaksMs"/>); the SPLINTERS fly out with the ring from a frame after, turning a little, and are the LAST
    /// to fade (<see cref="ShardsMs"/>); the Shadow HAZE spreads and thins (<see cref="BurstMs"/>, the whole impact).
    /// </summary>
    public float FlashMs { get; init; } = 200f;

    /// <inheritdoc cref="FlashMs"/>
    public float SlashMs { get; init; } = 84f;

    /// <inheritdoc cref="FlashMs"/>
    public float RingMs { get; init; } = 260f;

    /// <inheritdoc cref="FlashMs"/>
    public float StreaksMs { get; init; } = 220f;

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

    /// <summary>On the snap frame the hot teeth flare this far toward <see cref="FlashColor"/> (white-hot).</summary>
    public float SnapFlare { get; init; } = 0.5f;

    /// <summary>
    /// The teeth's glow, drawn in the material batch with a zero alpha (it only adds light), a share of their opacity; it
    /// builds with the charge from <see cref="GlowAtFormed"/> of it (whole on the snap), so the formed teeth stay a pale
    /// lavender-grey and the snap is the brightest moment.
    /// </summary>
    public float GlowShare { get; init; } = 0.45f;

    /// <inheritdoc cref="GlowShare"/>
    public float GlowAtFormed { get; init; } = 0.2f;

    /// <summary>The impact's colours: a white flash with a violet cast, a magenta slash and ring, pale speed lines, violet-magenta splinters, a violet Shadow haze (a dark puff was lost against the dark pack).</summary>
    public Color FlashColor { get; init; } = new(255, 236, 255);

    /// <inheritdoc cref="FlashColor"/>
    public Color SlashColor { get; init; } = new(226, 96, 240);

    /// <inheritdoc cref="FlashColor"/>
    public Color RingColor { get; init; } = new(214, 84, 236);

    /// <inheritdoc cref="FlashColor"/>
    public Color StreakColor { get; init; } = new(236, 228, 255);

    /// <inheritdoc cref="FlashColor"/>
    public Color ShardColor { get; init; } = new(200, 90, 235);

    /// <inheritdoc cref="FlashColor"/>
    public Color PuffColor { get; init; } = new(86, 50, 130);

    // ── SOUND, FLASH AND THE SCREEN'S GENERIC CUES ───────────────────────────────────────────────

    /// <summary>
    /// The reaction's ONE cue: a short dark, dry, sharp Shadow bite / thorn impact, its transient exactly on the SNAP
    /// (<see cref="SnapAtMs"/>, the rows meet). No secondary ticks, no metal trap, no swarm, no bone crunch.
    /// </summary>
    public IReadOnlyList<string> SnapCues { get; init; } = new[] { "sfx_seeker_jaws_fangs", "sfx_hit" };

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
