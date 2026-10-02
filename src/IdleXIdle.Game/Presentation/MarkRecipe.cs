using System;
using System.Collections.Generic;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A PERSISTENT TARGET-ATTACHED STATE IS PRESENTED (ADR-011, the MARK archetype; ADR-013, its production path): a
/// skill whose whole meaning is a state ON an enemy. BRAND is the reference (the owner's brief, 2026-09-29): "That enemy
/// has been branded... It quietly persists there. When the mark deepens, it bites further inward. If it spreads or
/// transfers, the same mark migrates cleanly to the next target." Not a projectile, not a field, not a trap, not a
/// champion performance.
/// </summary>
/// <remarks>
/// <para>
/// THE PICTURE is the curse (<see cref="Curse.CursePresentation"/>, the owner-approved "Living Shadow Corruption"):
/// separate infected territories of the body, depth told by how many. This recipe holds only the TIMING of the truth the
/// curse presents (<see cref="MarkPerformance"/>): the depth ladder, when a deepen is shown (after the host's own bite
/// settles), how a transfer and a SPRAWL spread are scheduled, and when the mark gives way to an action, a reaction or
/// the field's crush. The etched cut's atlas and its drawing values are gone (ADR-013 §8).
/// </para>
/// <para>
/// THE MOMENTS, all read from the fight (BRAND is a field: its Aura tick opens the mark, its Marked event reports the
/// depth in force, and the host is the front creature Core amplifies, or every creature under SPRAWL): APPLY on the
/// wave's first tick; DEEPEN when the shown stage steps up (three visible depths quantizing the gameplay depths);
/// TRANSFER when the host falls (the new front waits, then the mark seeps in); SPREAD under SPRAWL (the row receives it
/// near-simultaneously, never in lockstep). BRAND has no consume.
/// </para>
/// </remarks>
public sealed class MarkRecipe
{
    /// <summary>A name for traces and tests.</summary>
    public string Id { get; init; } = "";

    /// <summary>The depth stages: the spread (under the first floor), then THREE VISIBLE DEPTHS (marked, deeper, fully
    /// branded). The gameplay depths are untouched; the picture is quantized (the player reads marked, deeper, fully
    /// branded, never a percentage).</summary>
    public int Stages { get; init; } = 4;

    // ── THE AUTHORED BODY POINTS' HEAD CLEARANCE (data: tools/asset-pipeline/v2/mark_points.py) ────────────────────

    /// <summary>The extent, as a share of the creature's height, the authored body points (<c>.mark.json</c> "halo") were
    /// placed to keep clear of the creature's head: the curse's territories hang from those points.</summary>
    public float HaloWidthShare { get; init; } = 0.42f;

    /// <inheritdoc cref="HaloWidthShare"/>
    public float HaloHeightShare { get; init; } = 0.35f;

    // ── THE DEPTH LADDER: a stage is a function of the depth IN FORCE (the Marked event's percent) ───────────────────

    /// <summary>
    /// The lowest percent of each stage above the first: under 50 the SPREAD (SPRAWL's half strength); DEPTH 1 from
    /// 50 (BRAND's +70, ETCH's 120), DEPTH 2 from 150 (ETCH's 170, SINK's 150), DEPTH 3 from 200 (ETCH's 220 and 240,
    /// SINK's 230, GRAVEN to 320: nothing is added past it). The same depth is always the same picture, whichever
    /// variation reached it.
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

    // ── APPLY (u = playhead - the wave's first tick: the mark is true ON the tick) ──────────────────────────────────

    /// <summary>The mark begins to gather on the body this long before the first tick (its bloom starts on the tick).</summary>
    public float GatherFromMs { get; init; } = -230f;

    // ── DEEPEN ─────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A deepen's step: a deeper stage is SHOWN two steps after its settled start (the curse's new territory then
    /// travels and blooms); a SPRAWL row ripples a step and a half apart.</summary>
    public float CarveStepMs { get; init; } = 40f;

    // ── MIGRATE (u = playhead - the host's fall) and SPREAD ────────────────────────────────────────────────────────

    /// <summary>A transfer's arrival waits this long after a bite on the new front (not the deepen's
    /// <see cref="SettleMs"/>): past the bite's clip AND the reaction it draws (JAWS snaps ~333 ms after the contact and
    /// its white-hot flash has cooled by ~500), still clear of the next bite's wind-up.</summary>
    public float TransferSettleMs { get; init; } = 520f;

    /// <summary>SPRAWL: each further creature's hop leaves the source this long after the one before (near-simultaneous,
    /// never in lockstep).</summary>
    public float SpreadStaggerMs { get; init; } = 40f;

    /// <summary>When the mark leaves the fallen host, and how long it takes to reach the new one (a SPRAWL hop: three
    /// quarters of it).</summary>
    public float FlightFromMs { get; init; } = 60f;

    /// <inheritdoc cref="FlightFromMs"/>
    public float FlightMs { get; init; } = 240f;

    /// <summary>A migrated mark is whole this long after it lands (a deepen it carried is shown no sooner).</summary>
    public float ReformMs { get; init; } = 150f;

    /// <summary>SPRAWL: the first hop leaves the source this long after the first tick.</summary>
    public float SpreadFromMs { get; init; } = 20f;

    /// <summary>A creature SPRAWL's hop never reaches waits with this much of the ramp already behind it (it carries the
    /// faint shade until it falls).</summary>
    public float WaitingRampMs { get; init; } = 400f;

    // ── QUIET: the mark never outshines an action, a reaction or the field's crush ─────────────────────────────────

    /// <summary>An action's contact inside this window around a tick makes its beat quiet.</summary>
    public float QuietBeforeMs { get; init; } = 200f;

    /// <inheritdoc cref="QuietBeforeMs"/>
    public float QuietAfterMs { get; init; } = 350f;

    /// <summary>A presented reaction (JAWS) inside this window around a tick makes its beat quiet.</summary>
    public float YieldBeforeMs { get; init; } = 700f;

    /// <inheritdoc cref="YieldBeforeMs"/>
    public float YieldAfterMs { get; init; } = 400f;

    // ── WHERE ON THE BODY, AND WHEN ────────────────────────────────────────────────────────────────────────────────

    /// <summary>No deepen is shown over a bite: it waits until this long after a creature's strike (the attack clip's
    /// end: the whelp's lunge and recovery) when the strike lands within <see cref="SettleBeforeMs"/> of it (the
    /// wind-up). The depth is true on the tick all the same; it is shown on a settled body.</summary>
    public float SettleMs { get; init; } = 300f;

    /// <inheritdoc cref="SettleMs"/>
    public float SettleBeforeMs { get; init; } = 150f;

    /// <summary>A migrated mark that deepened on the way is deepened no sooner than this after the fall that sent it:
    /// the fallen creature's white death smoke drifts over the next host until then (measured +600..+933 ms).</summary>
    public float DeathClearMs { get; init; } = 950f;
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
