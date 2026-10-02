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

    // ── THE CURSE'S VOICE (design/audio/seeker-brand-audio-brief.md; scheduled once per wave by MarkVoice) ─────────
    //
    // Seven files of ONE family, built by tools/asset-pipeline/make_brand_cues.py from CC0 foley
    // (tools/asset-pipeline/foley/brand_curse/SOURCES.md). APPROVED (the owner, 2026-10-02): Candidate A, SUBTLE /
    // INTERNAL, is the canonical and only shipped set; its seven files' bytes are pinned by SHA-256 in brand_audio_test
    // (and foley/brand_curse/brand_audio_manifest.json): a different file is a new approval, never a regeneration. B and
    // C were rejected and live only in tools/asset-pipeline/audio_history/brand_curse/ (archive, never played). Every
    // cue is presentation only (asked on the picture's own moments), never lead (an authored action's duck keeps it
    // secondary), and quieter than SPRAY, HARD HANDS, JAWS and PRESS's crush. The idle curse is SILENT: no loop.

    /// <summary>APPLY: the first infection (the first territory blooms on the first tick): a reverse / suction lead-in, a
    /// dark internal take-hold, a short corruption tail. The first key that exists plays.</summary>
    public IReadOnlyList<string> ApplyCues { get; init; } = new[] { "sfx_seeker_brand_apply" };

    /// <summary>DEEPEN to depth 2: the corruption wakes, crawls under the skin (the picture's 170 ms travel) and takes hold
    /// at the new territory. One cue per SHOWN step, never one per territory.</summary>
    public IReadOnlyList<string> DeepenCues { get; init; } = new[] { "sfx_seeker_brand_deepen" };

    /// <summary>DEEPEN that reaches depth 3 (a multi-depth jump is ONE such cue): the deepen with one more corruption
    /// layer, never louder; it falls back to the plain deepen.</summary>
    public IReadOnlyList<string> DeepenDeepCues { get; init; } = new[] { "sfx_seeker_brand_deepen_deep", "sfx_seeker_brand_deepen" };

    /// <summary>SPRAWL: one victim takes the curse (a short, quieter relative of the apply's take-hold), one per victim at
    /// its landing, stepping down the row (<see cref="InfectStepShare"/>, <see cref="InfectStepPitch"/>).</summary>
    public IReadOnlyList<string> InfectCues { get; init; } = new[] { "sfx_seeker_brand_infect" };

    /// <summary>A cursed host falls (a transfer's start, or the curse's final collapse): a brief flare, an inward collapse,
    /// an ash exhale; secondary to the death sound. Several hosts falling on one frame are ONE cue.</summary>
    public IReadOnlyList<string> LeaveCues { get; init; } = new[] { "sfx_seeker_brand_leave" };

    /// <summary>A transfer's landing: a delayed internal inhale, then a short take-hold as the territories bloom. No travel
    /// sound: the gap between the leave and this is silence.</summary>
    public IReadOnlyList<string> AwakenCues { get; init; } = new[] { "sfx_seeker_brand_awaken" };

    /// <summary>THE ASH-BURN ACCENT, layered on a take-hold or a leave when the host's baked Ash-Burn weight is above 0 (a
    /// dry crumble): a continuous rule of the baked host data, never a name.</summary>
    public IReadOnlyList<string> AshCues { get; init; } = new[] { "sfx_seeker_brand_ash" };

    /// <summary>The cues' volumes. Each file is mastered so that its K-weighted loudest 50 ms x this volume x the SFX
    /// master 0.8 meets the brief's target, which the integration's mix check (the fight's own soundtrack, 2026-10-02)
    /// lowered by 1 dB for every cue alike (the three candidates were level-matched for the review) so the loudest, an apply on a host with no
    /// Ash-Burn, sits >= 3 dB under PRESS's tick (-31.3): apply -35, deepen -36, deepen_deep -35.5, infect -39, leave -37
    /// (under the enemy's death), awaken -36, the ash accent -41. Every one under PRESS's
    /// <see cref="FieldRecipe.TickVolume"/>, JAWS' snap and the action contacts. make_brand_cues.py mirrors them (VOLUME /
    /// TARGET): change both together.</summary>
    public float ApplyVolume { get; init; } = 0.183f;

    /// <inheritdoc cref="ApplyVolume"/>
    public float DeepenVolume { get; init; } = 0.163f;

    /// <inheritdoc cref="ApplyVolume"/>
    public float DeepenDeepVolume { get; init; } = 0.173f;

    /// <inheritdoc cref="ApplyVolume"/>
    public float InfectVolume { get; init; } = 0.116f;

    /// <inheritdoc cref="ApplyVolume"/>
    public float LeaveVolume { get; init; } = 0.145f;

    /// <inheritdoc cref="ApplyVolume"/>
    public float AwakenVolume { get; init; } = 0.163f;

    /// <inheritdoc cref="ApplyVolume"/>
    public float AshVolume { get; init; } = 0.092f;

    /// <summary>The K-weighted loudest 50 ms (dB, x the SFX master 0.8) each file is mastered to at its volume above:
    /// make_brand_cues.py's TARGET, mirrored here (a test holds the two together) so the Ash accent can be kept under
    /// the cue it rides on (<see cref="AshShareUnder"/>).</summary>
    public float ApplyTargetDb { get; init; } = -35f;

    /// <inheritdoc cref="ApplyTargetDb"/>
    public float DeepenTargetDb { get; init; } = -36f;

    /// <inheritdoc cref="ApplyTargetDb"/>
    public float DeepenDeepTargetDb { get; init; } = -35.5f;

    /// <inheritdoc cref="ApplyTargetDb"/>
    public float InfectTargetDb { get; init; } = -39f;

    /// <inheritdoc cref="ApplyTargetDb"/>
    public float LeaveTargetDb { get; init; } = -37f;

    /// <inheritdoc cref="ApplyTargetDb"/>
    public float AwakenTargetDb { get; init; } = -36f;

    /// <inheritdoc cref="ApplyTargetDb"/>
    public float AshTargetDb { get; init; } = -41f;

    /// <summary>THE ACCENT IS NEVER LOUDER THAN ITS LAYER: at any burn the Ash accent sits at least this far (dB) under
    /// the main cue it rides on (<see cref="AshShareUnder"/>).</summary>
    public float AshUnderMainDb { get; init; } = 2f;

    /// <summary>
    /// The share of <see cref="AshVolume"/> the accent plays at under a <paramref name="main"/> cue, constant in burn (so
    /// more burn is always more ash). With the Ash-Burn rule the main cue is <c>T_main + 20 log(1 - AshMainCut b)</c>
    /// and the accent <c>T_ash + 20 log(b s)</c>; their gap is smallest at burn 1, so <c>s = min(1, 10^((T_main - T_ash
    /// - AshUnderMainDb) / 20) x (1 - AshMainCut))</c> keeps it >= <see cref="AshUnderMainDb"/> at every burn: 1 under an
    /// apply, deepen or awaken, ~0.94 under a leave, 0.75 under an infect (at full it would be 0.5 dB OVER a burn-1
    /// infect).
    /// </summary>
    public float AshShareUnder(MarkCueKind main)
        => main == MarkCueKind.Ash ? 0f
         : Math.Min(1f, MathF.Pow(10f, (TargetDbOf(main) - AshTargetDb - AshUnderMainDb) / 20f) * (1f - AshMainCut));

    /// <summary>A kind's mastered loudness target (dB).</summary>
    public float TargetDbOf(MarkCueKind kind) => kind switch
    {
        MarkCueKind.Apply => ApplyTargetDb,
        MarkCueKind.Deepen => DeepenTargetDb,
        MarkCueKind.DeepenDeep => DeepenDeepTargetDb,
        MarkCueKind.Infect => InfectTargetDb,
        MarkCueKind.Leave => LeaveTargetDb,
        MarkCueKind.Awaken => AwakenTargetDb,
        _ => AshTargetDb,
    };

    /// <summary>
    /// Where each cue's TAKE-HOLD transient sits inside its file (ms): the file is authored to it, and the cue starts this
    /// long before its moment on the picture (<c>start = moment - offset</c>; the frame's 60 Hz step lands it up to a
    /// frame late, as PRESS's thump). The brief's offsets: apply 180, deepen and deepen_deep 230, infect 15, leave 10 (the
    /// flare on the fall, 0..20), awaken 120, ash 5 (0..10).
    /// </summary>
    public float ApplyCueStartMs { get; init; } = 180f;

    /// <inheritdoc cref="ApplyCueStartMs"/>
    public float DeepenCueStartMs { get; init; } = 230f;

    /// <inheritdoc cref="ApplyCueStartMs"/>
    public float DeepenDeepCueStartMs { get; init; } = 230f;

    /// <inheritdoc cref="ApplyCueStartMs"/>
    public float InfectCueStartMs { get; init; } = 15f;

    /// <inheritdoc cref="ApplyCueStartMs"/>
    public float LeaveCueStartMs { get; init; } = 10f;

    /// <inheritdoc cref="ApplyCueStartMs"/>
    public float AwakenCueStartMs { get; init; } = 120f;

    /// <inheritdoc cref="ApplyCueStartMs"/>
    public float AshCueStartMs { get; init; } = 5f;

    /// <summary>
    /// The picture's decisive frame each take-hold lands on, after its event (ms): the apply's flare peaks 60..160 after
    /// the first tick (60); a deepen's take-hold is ~230 after its shown step (the wake 0..80, the travel 0..170, the first
    /// new bloom at +170); an infect takes hold on its landing (15); a leave flares on the fall (10); an awaken takes hold
    /// as the arrival's territories bloom (120 after the landing). So: apply starts 120 ms before the tick, a deepen on its
    /// step, an infect on its landing, a leave on the fall, an awaken on its landing.
    /// </summary>
    public float ApplyTakeHoldMs { get; init; } = 60f;

    /// <inheritdoc cref="ApplyTakeHoldMs"/>
    public float DeepenTakeHoldMs { get; init; } = 230f;

    /// <inheritdoc cref="ApplyTakeHoldMs"/>
    public float InfectTakeHoldMs { get; init; } = 15f;

    /// <inheritdoc cref="ApplyTakeHoldMs"/>
    public float LeaveTakeHoldMs { get; init; } = 10f;

    /// <inheritdoc cref="ApplyTakeHoldMs"/>
    public float AwakenTakeHoldMs { get; init; } = 120f;

    /// <summary>A cue whose start the playhead passed by more than this (a seek, a long hitch) is skipped, never played
    /// late (PRESS's rule).</summary>
    public float CueLateMs { get; init; } = 30f;

    /// <summary>A cue's share near an action (its Skill within <see cref="QuietBeforeMs"/> / <see cref="QuietAfterMs"/>
    /// of the take-hold, the field's crush on its tick, or while the champion performs) and near a presented reaction's
    /// snap (<see cref="YieldBeforeMs"/> / <see cref="YieldAfterMs"/>). Already under an action's duck, the duck alone
    /// applies, never duck x quiet.</summary>
    public float CueQuietShare { get; init; } = 0.6f;

    /// <inheritdoc cref="CueQuietShare"/>
    public float CueYieldShare { get; init; } = 0.5f;

    /// <summary>THE ASH-BURN RULE: the main cue loses this share of its volume per unit of the host's Ash-Burn weight
    /// (x(1 - 0.25 burn)) and the ash accent plays at <see cref="AshVolume"/> x burn; giving way to a reaction, the accent
    /// (the least important layer) is omitted and the main cue keeps its whole volume.</summary>
    public float AshMainCut { get; init; } = 0.25f;

    /// <summary>SPRAWL: each further victim's infect plays at this share of the one before (x0.88 per victim) and this much
    /// lower (octaves per victim), so the row reads as one phrase travelling away.</summary>
    public float InfectStepShare { get; init; } = 0.88f;

    /// <inheritdoc cref="InfectStepShare"/>
    public float InfectStepPitch { get; init; } = -0.04f;

    /// <summary>Hosts falling within this of the first (one 60 Hz frame: a SPRAWL row kill) give ONE leave.</summary>
    public float LeaveMergeMs { get; init; } = 1000f / 60f;

    /// <summary>A SPRAWL deepen ripples down the row (a step and a half per place: <c>chainOrder x</c>
    /// <see cref="CarveStepMs"/><c> x 1.5</c>, ~60 ms): one tick's shown steps on DIFFERENT creatures within the whole
    /// row's ripple (<c>(slots - 1) x CarveStepMs x 1.5</c>, the bound <see cref="MarkPerformance"/>'s settle uses) plus
    /// this slack (two 60 Hz frames: the lattice the steps are found on) are ONE deepen for the ear (deep if any reaches
    /// depth 3). Only in SPRAWL, and never two steps of one creature: each of its shown steps is its own cue.</summary>
    public float DeepenRippleSlackMs { get; init; } = 2000f / 60f;

    /// <summary>How far a cue pans with its host's position (the reactions' width).</summary>
    public float CuePanWidth { get; init; } = 0.35f;

    /// <summary>Per-play pitch variation (octaves).</summary>
    public float CueVary { get; init; } = 0.03f;

    /// <summary>The cue chain of a scheduled cue's kind.</summary>
    public IReadOnlyList<string> CuesOf(MarkCueKind kind) => kind switch
    {
        MarkCueKind.Apply => ApplyCues,
        MarkCueKind.Deepen => DeepenCues,
        MarkCueKind.DeepenDeep => DeepenDeepCues,
        MarkCueKind.Infect => InfectCues,
        MarkCueKind.Leave => LeaveCues,
        MarkCueKind.Awaken => AwakenCues,
        _ => AshCues,
    };

    /// <summary>The full volume of a scheduled cue's kind (before the quiet, yield, Ash-Burn and SPRAWL shares).</summary>
    public float VolumeOf(MarkCueKind kind) => kind switch
    {
        MarkCueKind.Apply => ApplyVolume,
        MarkCueKind.Deepen => DeepenVolume,
        MarkCueKind.DeepenDeep => DeepenDeepVolume,
        MarkCueKind.Infect => InfectVolume,
        MarkCueKind.Leave => LeaveVolume,
        MarkCueKind.Awaken => AwakenVolume,
        _ => AshVolume,
    };

    /// <summary>Where a kind's take-hold sits inside its file (ms).</summary>
    public float CueStartOf(MarkCueKind kind) => kind switch
    {
        MarkCueKind.Apply => ApplyCueStartMs,
        MarkCueKind.Deepen => DeepenCueStartMs,
        MarkCueKind.DeepenDeep => DeepenDeepCueStartMs,
        MarkCueKind.Infect => InfectCueStartMs,
        MarkCueKind.Leave => LeaveCueStartMs,
        MarkCueKind.Awaken => AwakenCueStartMs,
        _ => AshCueStartMs,
    };

    /// <summary>Is a kind played past the bank's repeat throttle? The SPRAWL infects (40 ms apart, bounded by the victims of
    /// one spread) and the ash accent (bounded by the cue it rides on); mirrored in film_audio.py's UNTHROTTLED.</summary>
    public static bool Unthrottled(MarkCueKind kind) => kind is MarkCueKind.Infect or MarkCueKind.Ash;
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
