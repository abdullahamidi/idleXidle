using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A PERSISTENT TARGET-ATTACHED STATE IS PRESENTED (ADR-011, the MARK archetype): a skill whose whole meaning is a
/// state ON an enemy. BRAND is the reference (the owner's brief, 2026-09-29): "That enemy has been branded. The mark is
/// living shadow, etched into its body. It quietly persists there. When the mark deepens, it bites further inward. If it
/// spreads or transfers, the same mark migrates cleanly to the next target." Not a projectile, not a field, not a trap,
/// not a champion performance.
/// </summary>
/// <remarks>
/// <para>
/// THE ETCHED SHADOW CUT (the second pass, 2026-09-30; the first slice's lit lavender spiral read as an eye, a rune, a
/// glowing line on top): a BROKEN ASYMMETRIC SCAR-SPIRAL (the first slice's coil gesture, from a PixelLab silhouette,
/// redrawn by tools/asset-pipeline/v2/seeker_brand_cut.py: its outer contour broken, its weight on one side, its centre
/// never a dot, small scar branches) cut INTO the host's body at its torso. DARK FIRST, LIGHT SECOND: the incision is
/// near-black with dark-violet walls and ONE lit rim on its light-facing (upper-left) edge, a minority of the mark; a dark
/// ink stain round it (the lit smoke kept faint). On a dark body the rim carries it, on a light body the incision does.
/// It is drawn in the creature loop, right after the creature and before its hit flash, so a creature standing in front
/// covers it and a hit whitens body and brand together.
/// </para>
/// <para>
/// THE MOMENTS, all read from the fight (BRAND is a field: its Aura tick opens the mark, its Marked event reports the
/// depth in force, and the host is the front creature Core amplifies, or every creature under SPRAWL):
/// APPLY on the wave's first tick (ink motes gather, a dark stain condenses into the cut, its rim and outer edge catch
/// ON the tick and it settles dark within ~200 ms); IDLE (the same cut; the stain drifts by a texel, one edge shimmers
/// once in six steps; no pulse, no travelling light); REFRESH (a tick that changes nothing shows nothing); DEEPEN
/// (THREE VISIBLE DEPTHS -- marked, deeper, fully branded -- quantizing the gameplay depths: the existing cut widens,
/// its inner curl runs on, scar branches appear, the stain gains density; the phrase: ink gathers where the new cut will
/// be, the new cut appears, its rim answers, settled by ~170 ms, after the host's own bite; never brighter, never a
/// flash); TRANSFER (the host falls: the old mark collapses into its own dark centre while the death presents; the new
/// front, which Core marks at once, carries a dark stain and the same mark seeps in on it; no bridge between the bodies);
/// SPREAD (SPRAWL: the source's mark branches short ink threads out to the row, which etches near-simultaneously). BRAND
/// has no consume: none is drawn.
/// </para>
/// </remarks>
public sealed class MarkRecipe
{
    /// <summary>A name for traces and tests.</summary>
    public string Id { get; init; } = "";

    // ── THE PARTS (tools/asset-pipeline/v2/seeker_brand_cut.py; keypose_sources/seeker_brand_spans.json pins the cells) ──

    /// <summary>
    /// ONE atlas, so a brand costs no texture switch: the CUT cells in <see cref="Stages"/> rows of <see cref="Columns"/>
    /// cells (a row per depth stage), the HALO cells in the same layout from row <see cref="HaloRow0"/>, and the THREAD
    /// puffs at <see cref="ThreadY"/>.
    /// </summary>
    public string AtlasKey { get; init; } = "fxp_seeker_brand";

    /// <summary>One cell's size, runtime px (28 logical px of 3).</summary>
    public int Cell { get; init; } = 84;

    /// <summary>The depth stages, one atlas row each: the spread cut, then THREE VISIBLE DEPTHS (marked, deeper, fully
    /// branded). The gameplay depths are untouched; the picture is quantized (the player reads marked, deeper, fully
    /// branded, never a percentage).</summary>
    public int Stages { get; init; } = 4;

    /// <summary>The cells per stage: gather x3, idle x6, edge, carve x3, form x3, loosen x2.</summary>
    public int Columns { get; init; } = 18;

    /// <summary>The first HALO row.</summary>
    public int HaloRow0 { get; init; } = 4;

    /// <summary>The THREAD puffs' row, runtime px from the top.</summary>
    public int ThreadY { get; init; } = 672;

    /// <summary>One thread puff's cell, runtime px.</summary>
    public int ThreadCell { get; init; } = 24;

    /// <summary>The coil's visual centre inside a cell, runtime px: the point laid on the body's torso.</summary>
    public Vector2 Centre { get; init; } = new(CentreX, CentreY);

    /// <summary>The coil's outer tip inside a cell, runtime px: where a migrating mark lands and the coil is drawn in from.</summary>
    public Vector2 Tip { get; init; } = new(TipX, TipY);

    /// <summary>The coil's box at scale 1, runtime px.</summary>
    public float CoilBox { get; init; } = 60f;

    /// <summary>seeker_brand_spans.json "centre" and "tip".</summary>
    public const float CentreX = 44.0f, CentreY = 40.6f, TipX = 44.1f, TipY = 64.0f;

    /// <summary>Column of the first GATHER cell.</summary>
    public const int Gather0 = 0;

    /// <summary>Column of the first IDLE cell; the swirl's <see cref="IdlePhases"/> phases follow.</summary>
    public const int Idle0 = 3;

    /// <summary>The swirl's phases (idle cells).</summary>
    public const int IdlePhases = 6;

    /// <summary>Column of the EDGE cell (the whole coil's lit line: the APPLY beat).</summary>
    public const int Edge = 9;

    /// <summary>Column of the first CARVE cell (the DEEPEN beat's chisel, three steps travelling inward).</summary>
    public const int Carve0 = 10;

    /// <summary>Column of the first FORM cell (the coil drawn in from its tip: a migrated mark's arrival).</summary>
    public const int Form0 = 13;

    /// <summary>Column of the first LOOSEN cell.</summary>
    public const int Loosen0 = 16;

    // ── SIZE: a small-to-medium state, constant (depth never changes the size) ───────────────────────────────────────

    /// <summary>The coil's box as a share of the host's canonical visible height (a whelp's ~60 px).</summary>
    public float SizeShare { get; init; } = 0.28f;

    /// <summary>The mark's whole drawn extent (its smoky halo, the gather and the re-form; the loosen excepted, which plays
    /// on a falling body) as a share of the creature's height, at the largest the snap to thirds can make it: what must
    /// stay clear of the creature's head (<c>.mark.json</c> "halo", checked against the atlas by the tests).</summary>
    public float HaloWidthShare { get; init; } = 0.42f;

    /// <inheritdoc cref="HaloWidthShare"/>
    public float HaloHeightShare { get; init; } = 0.35f;

    /// <summary>The smallest and largest scale; a scale is snapped to thirds so a logical pixel is whole screen pixels.</summary>
    public float MinScale { get; init; } = 2f / 3f;

    /// <inheritdoc cref="MinScale"/>
    public float MaxScale { get; init; } = 3f;

    // ── THE DEPTH LADDER: a stage is a function of the depth IN FORCE (the Marked event's percent) ───────────────────

    /// <summary>
    /// The lowest percent of each stage above the first: under 50 the SPREAD cut (SPRAWL's half strength); DEPTH 1 from
    /// 50 (BRAND's +70, ETCH's 120), DEPTH 2 from 150 (ETCH's 170, SINK's 150), DEPTH 3 from 200 (ETCH's 220 and 240,
    /// SINK's 230, GRAVEN to 320: the final readable silhouette, nothing is added past it). The same depth is always the
    /// same picture, whichever variation reached it.
    /// </summary>
    public IReadOnlyList<int> StageFloors { get; init; } = new[] { 50, 150, 200 };

    /// <summary>The stage a depth in force is drawn at (0 spread .. <see cref="Stages"/> - 1).</summary>
    public int StageOf(int percent)
    {
        var s = 0;
        for (var i = 0; i < StageFloors.Count; i++)
            if (percent >= StageFloors[i]) s = i + 1;
        return Math.Min(s, Stages - 1);
    }

    // ── PALETTE (the art is grey; these tint it; never white) ──────────────────────────────────────────────────────

    /// <summary>The cut's tint. The atlas bakes its value: the incision near-black (~11 luma: a cut filled with Shadow,
    /// darker than a light host), its walls dark violet (~42), and ONE lit rim on the light-facing edge (~116): on a dark
    /// body the rim carries the mark, on a light body the incision does.</summary>
    public Color Groove { get; init; } = new(136, 108, 196);

    /// <summary>A beat's lit line (the apply, the chisel, the re-cut): a paler lavender drawn OVER the groove (alpha, not
    /// light, so it can never saturate to white).</summary>
    public Color Hot { get; init; } = new(226, 208, 255);

    /// <summary>The halo drawn as INK: near-black violet (JAWS' mist core), the char that reads on a light body.</summary>
    public Color Ink { get; init; } = new(12, 7, 20);

    /// <summary>The halo's opacity as ink.</summary>
    public float InkAlpha { get; init; } = 0.85f;

    /// <summary>The halo drawn again as lit SMOKE: a lavender-grey, kept faint (the stain is dark first; lit smoke round
    /// the first slice's line made it a glow).</summary>
    public Color Smoke { get; init; } = new(110, 92, 160);

    /// <summary>The halo's opacity as smoke.</summary>
    public float SmokeAlpha { get; init; } = 0.18f;

    // ── APPLY (u = playhead - the wave's first tick: the cut is true ON the tick) ───────────────────────────────────

    /// <summary>Smoke begins to gather on the body (GATHER cell 0).</summary>
    public float GatherFromMs { get; init; } = -230f;

    /// <summary>GATHER cell 1 from here, cell 2 from <see cref="Gather2Ms"/>, the coil itself from <see cref="FormMs"/>.</summary>
    public float Gather1Ms { get; init; } = -160f;

    /// <inheritdoc cref="Gather1Ms"/>
    public float Gather2Ms { get; init; } = -95f;

    /// <inheritdoc cref="Gather1Ms"/>
    public float FormMs { get; init; } = -30f;

    /// <summary>The whole cut's lit line on the tick that applies it: its opacity and how long it takes to cool (at 0.8 the
    /// apply is the brightest thing the brand does, and still inside the range of PRESS's crush: 18_measures.md).</summary>
    public float EtchAlpha { get; init; } = 0.8f;

    /// <inheritdoc cref="EtchAlpha"/>
    public float EtchMs { get; init; } = 200f;

    /// <summary>A quiet beat's share of the lit line (an action's contact, a JAWS bite or a PRESS crush close by).</summary>
    public float QuietShare { get; init; } = 0.5f;

    // ── IDLE ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The swirl's step: the six idle cells in turn (the lit third crawling round the coil), stepped like a pixel
    /// animation, never cross-faded; each creature's steps fall at its own moment (never the whole row in lockstep). At 400
    /// ms it was the loudest thing the idle did and the coil read as a spinner; at 800 (with the smoke roots held still)
    /// the cut itself is what persists.</summary>
    public float SwirlStepMs { get; init; } = 800f;

    // ── DEEPEN ─────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The DEEPEN phrase, three steps then a short cooling: ink gathers where the new cut will be (0), the new
    /// cut appears with its rim (1, the deeper depth shown from its end), the rim answers (2), settled by ~170 ms. No
    /// flash, no pulse ring: the same wound became deeper.</summary>
    public float CarveStepMs { get; init; } = 40f;

    /// <summary>The chisel's opacity, and how long its last step (the new cut) takes to cool.</summary>
    public float DeepenEdgeAlpha { get; init; } = 0.8f;

    /// <inheritdoc cref="DeepenEdgeAlpha"/>
    public float DeepenEdgeMs { get; init; } = 50f;

    /// <summary>A depth that rose inside one stage (WINNOW's +10): the chisel runs again, faintly.</summary>
    public float RetraceAlpha { get; init; } = 0.3f;

    // ── MIGRATE (u = playhead - the host's fall) and SPREAD ────────────────────────────────────────────────────────

    /// <summary>The coil coming apart on the fallen host (LOOSEN cells 0 then 1, the second cooling), pinned at its
    /// STANDING torso on the death clip; the killing blow's flash covers its first ~120 ms.</summary>
    public float LoosenMs { get; init; } = 220f;

    /// <summary>A TRANSFER draws no bridge (the default): the old mark collapses into its own dark centre on the falling
    /// host while the death presents, and the same mark seeps in on the new front; a strand competed with the death smoke
    /// and passed the next creature's face. SPRAWL's spread keeps its short ink threads.</summary>
    public bool TransferThread { get; init; }

    /// <summary>A transfer's etch-in waits this long after a bite on the new front (not the deepen's
    /// <see cref="SettleMs"/>): past the bite's clip AND the reaction it draws (JAWS snaps ~333 ms after the contact and
    /// its white-hot flash has cooled by ~500), still clear of the next bite's wind-up.</summary>
    public float TransferSettleMs { get; init; } = 520f;

    /// <summary>SPRAWL: each further creature's thread leaves the source this long after the one before (near-simultaneous:
    /// the source darkens, short ink threads branch outward, the row etches almost at once).</summary>
    public float SpreadStaggerMs { get; init; } = 40f;

    /// <summary>When the smoke leaves the fallen host, and how long its lead puff takes to reach the new coil's tip.</summary>
    public float FlightFromMs { get; init; } = 60f;

    /// <inheritdoc cref="FlightFromMs"/>
    public float FlightMs { get; init; } = 240f;

    /// <summary>The puffs carrying one mark: an even strand (no head, no fading tail: that grammar is a projectile's),
    /// each puff under a puff's VISIBLE width behind the one before at the strand's peak speed, so they overlap into one
    /// thread (<see cref="ThreadVisibleShare"/>).</summary>
    public int Beads { get; init; } = 7;

    /// <summary>The flight's path SAGS by this share of the distance, between <see cref="ArcMin"/> and <see cref="ArcMax"/>
    /// px, toward the new coil's tip: under the creatures' heads and eyes (a lift crossed the next creature's face and read
    /// as something shot at it).</summary>
    public float ArcLift { get; init; } = 0.18f;

    /// <summary>A thread puff's visible (opaque) width as a share of its cell: the strand spaces its puffs by it so they
    /// overlap into one thread, never a dotted row.</summary>
    public float ThreadVisibleShare { get; init; } = 0.4f;

    /// <inheritdoc cref="ArcLift"/>
    public float ArcMin { get; init; } = 14f;

    /// <inheritdoc cref="ArcLift"/>
    public float ArcMax { get; init; } = 30f;

    /// <summary>The coil drawn in along its path from the tip where the smoke landed (FORM cells 0..2); then, at the same
    /// depth, ONE faint re-cut (<see cref="ReformEtchAlpha"/>) once the fall's white death smoke has cleared off it and no
    /// bite is in the way (at the re-form itself the smoke covered it: the arrival's beat was never seen).</summary>
    public float ReformMs { get; init; } = 150f;

    /// <inheritdoc cref="ReformMs"/>
    public float ReformEtchAlpha { get; init; } = 0.5f;

    /// <summary>SPRAWL: the first thread leaves the source this long after the first tick.</summary>
    public float SpreadFromMs { get; init; } = 20f;

    /// <summary>The smoke a creature carries while the strand is still on its way to it (Core amplifies it at once): the
    /// halo at full smoke strength, thickening from <see cref="WaitingFloor"/> over the last <see cref="WaitingRampMs"/>
    /// before it lands (from a quarter, a creature waiting for its hop looked bare for ~300 ms).</summary>
    public float WaitingSmokeAlpha { get; init; } = 1f;

    /// <inheritdoc cref="WaitingSmokeAlpha"/>
    public float WaitingRampMs { get; init; } = 400f;

    /// <inheritdoc cref="WaitingSmokeAlpha"/>
    public float WaitingFloor { get; init; } = 0.5f;

    // ── QUIET: the mark never outshines an action, a reaction or the field's crush ─────────────────────────────────

    /// <summary>An action's contact inside this window around a tick makes its beat quiet.</summary>
    public float QuietBeforeMs { get; init; } = 200f;

    /// <inheritdoc cref="QuietBeforeMs"/>
    public float QuietAfterMs { get; init; } = 350f;

    /// <summary>A presented reaction (JAWS) inside this window around a tick makes its beat quiet.</summary>
    public float YieldBeforeMs { get; init; } = 700f;

    /// <inheritdoc cref="YieldBeforeMs"/>
    public float YieldAfterMs { get; init; } = 400f;

    // ── WHERE ON THE BODY ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>No chisel is traced over a bite: its travel waits until this long after a creature's strike (the attack
    /// clip's end: the whelp's lunge and recovery) when the strike lands within <see cref="SettleBeforeMs"/> of it (the
    /// wind-up). The depth is true on the tick all the same; the cut is traced on a settled body.</summary>
    public float SettleMs { get; init; } = 300f;

    /// <inheritdoc cref="SettleMs"/>
    public float SettleBeforeMs { get; init; } = 150f;

    /// <summary>A migrated coil that deepened on the way is cut no sooner than this after the fall that sent it: the
    /// fallen creature's white death smoke drifts over the next host until then (measured +600..+933 ms).</summary>
    public float DeathClearMs { get; init; } = 950f;

    /// <summary>The FALLBACK torso band (a strip with no authored body point, <see cref="MarkPoints"/>): its share of the
    /// opaque width from the side facing the champion.</summary>
    public float TorsoFrom { get; init; } = 0.25f;

    /// <inheritdoc cref="TorsoFrom"/>
    public float TorsoTo { get; init; } = 0.65f;
}

/// <summary>The mark recipes, by character and skill (ADR-011's contract: a presentation belongs to its SKILL).</summary>
public static class MarkRecipes
{
    /// <summary>THE SEEKER's BRAND (the MARK / PERSISTENT TARGET-ATTACHED STATE reference).</summary>
    public static readonly MarkRecipe SeekerBrand = new() { Id = "seeker.brand" };

    private static readonly Dictionary<(string Character, string Skill), MarkRecipe> BySkill = new()
    {
        [("seeker", "sign_brand")] = SeekerBrand,
    };

    /// <summary>
    /// The mark recipe this champion presents for this skill (a SkillDef.Id), or null: the skill keeps its generic
    /// presentation (for BRAND, the held field art). RH_ACTION_RECIPES=0 turns these off with the action recipes, and
    /// RH_MARK_RECIPES=0 turns off these alone (the before / after comparison).
    /// </summary>
    public static MarkRecipe? For(string characterId, string skillId)
        => ActionRecipes.Enabled && Enabled && BySkill.TryGetValue((characterId, skillId), out var own) ? own : null;

    /// <summary>RH_MARK_RECIPES=0 presents every mark the old way.</summary>
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("RH_MARK_RECIPES") is not ("0" or "false");
}

/// <summary>
/// WHICH FIELD IS PRESENTED HOW (ADR-011): a build may carry two fields (PRESS and BRAND, BRAND and MIRE...). A field
/// with its own field recipe is PERFORMED (PRESS), a field with a mark recipe is a MARK on its host (BRAND), and only a
/// field with neither keeps the generic held aura. Chosen by recipe, never by slot order: the first Field used to win,
/// so a BRAND woven before PRESS took PRESS's accepted picture away and drew a reticle behind the hunter instead.
/// </summary>
public static class FieldRoles
{
    /// <summary>The slots (or -1) of the performed field, the mark field and the field that keeps the held aura.</summary>
    public static (int Performed, int Mark, int Held) Choose(IReadOnlyList<IdleXIdle.Core.Builds.EquippedSkill> skills, string characterId)
    {
        int performed = -1, mark = -1, held = -1;
        for (var i = 0; i < skills.Count; i++)
        {
            var def = skills[i].Def;
            if (def.Kind != IdleXIdle.Core.Builds.SkillKind.Field) continue;
            if (performed < 0 && FieldRecipes.For(characterId, def.Id) is not null) performed = i;
            else if (mark < 0 && MarkRecipes.For(characterId, def.Id) is not null) mark = i;
            else if (held < 0 && FieldRecipes.For(characterId, def.Id) is null && MarkRecipes.For(characterId, def.Id) is null) held = i;
        }
        return (performed, mark, held);
    }
}
