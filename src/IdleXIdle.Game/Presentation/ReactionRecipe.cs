using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// HOW A REACTION IS PRESENTED (ADR-011, the REACTION archetype; the production direction of 2026-09-27): the world's
/// answer to an enemy's bite, drawn on its own layer and never on the champion's figure. JAWS is the reference and it
/// is SHADOW PIRANHA: when the fight resolves the reflected blow, several tiny Shadow jaw-heads appear ON the creature
/// that bit, dart in, CHOMP, and are gone. A stylised reactive-damage phrase of ~140 ms, in the family of thorns-style
/// retaliation: not a summoned pet, not a persistent creature, not a projectile, and not a mechanism the player has to
/// understand. The sentence is ENEMY HITS SEEKER → SMALL SHADOW JAWS APPEAR ON ATTACKER → CHOMP → "−X JAWS" → GONE.
/// </summary>
/// <remarks>
/// <para>
/// A Reaction takes no beat, so it owns no clip and is never an <see cref="IActionPerformance"/> (that interface is the
/// figure's owner). Everything is measured from the first frame that shows the enemy's contact (the bite's millisecond,
/// on which Core also resolves the reaction and its answer). Three jaws share ONE tiny source sprite in two states
/// (OPEN and SHUT); the runtime owns their position, rotation, scale, timing and fade, staggered a few ms apart so it
/// feels like a brief swarm bite, never three identical stamps on one frame. The answer (the reflected number, the
/// creature's flash, a kill's fall) lands on the MAIN CHOMP, ~25 ms in; the fight resolved it at the bite (t 0).
/// </para>
/// <para>
/// WHAT IS GONE FOR GOOD (the owner, 2026-09-27): the spring-loaded bear trap, its chain, its housing, the tether, the
/// yank, the reel-in, the bite glyphs on the champion and the mirrored incoming/outgoing snap. Their art stays on disk
/// as history and is never played. The champion does nothing: no full-body reaction animation, no receiver recoil, and
/// SPRAY, HARD HANDS and the basic swing are never interrupted. REPAY keeps its own legacy presentation (the lookup is
/// by SKILL id). The dock's REARMING / READY state is Core's <c>ReactionArmed</c> report and is untouched.
/// </para>
/// </remarks>
public sealed class ReactionRecipe
{
    /// <summary>A stable name for traces and tests.</summary>
    public required string Id { get; init; }

    // ── ART: one tiny source sprite in two states (tools/asset-pipeline/v2/seeker_piranha.py) ────

    /// <summary>The jaw-head with its mouth OPEN, facing LEFT (the mouth at the left edge), on a small canvas.</summary>
    public string OpenKey { get; init; } = "fxp_seeker_jaws_open";

    /// <summary>The same head with its mouth SHUT (the teeth met), the same canvas and facing.</summary>
    public string ShutKey { get; init; } = "fxp_seeker_jaws_shut";

    /// <summary>A soft streak (shared part): the target-local Shadow residue each bite leaves for a moment.</summary>
    public string SmearKey { get; init; } = "fxp_trail_soft";

    /// <summary>A soft dot (shared part): the chomp's small Shadow glint, in the light pass.</summary>
    public string GlintKey { get; init; } = "fxp_spark_dot";

    /// <summary>In the canvas (texture px, a 64 x 40 canvas): the mouth's bite point, the sprite's origin; the head rotates about it.</summary>
    public Vector2 MouthPoint { get; init; } = new(7f, 24f);

    /// <summary>The canvas HEIGHT (the head fills ~31 of its 40 rows): its on-screen size is a height.</summary>
    public float ArtHeight { get; init; } = 40f;

    // ── SIZE ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One jaw's height as a share of the bitten creature's visible height, clamped: small, a bite on part of the body,
    /// never a mouth around it. <see cref="ReactionRecipes.SizeDial"/> scales it for a comparison film.
    /// </summary>
    public float JawBodyShare { get; init; } = 0.28f;

    /// <inheritdoc cref="JawBodyShare"/>
    public float JawMinPx { get; init; } = 22f;

    /// <inheritdoc cref="JawBodyShare"/>
    public float JawMaxPx { get; init; } = 48f;

    // ── THE SWARM: three jaws, art-directed, never random ───────────────────────────────────────

    /// <summary>
    /// One jaw of the phrase: WHERE it bites (a share of the creature's visible body, x from the edge that faces the
    /// champion, y from the top), FROM which direction it arrives (a unit vector toward the bite point), its start
    /// offset (ms after the first frame), its size against the first jaw's, and whether it is drawn BEHIND the creature.
    /// </summary>
    public readonly record struct Jaw(Vector2 BiteShare, Vector2 From, float DelayMs, float Scale, bool Behind);

    /// <summary>
    /// The three: one arrives upper/front, one lower/front, one slightly behind and above (drawn behind the body, so
    /// the swarm has depth). Offsets 0 / 15 / 25 ms: their chomps overlap, a brief swarm bite, not three icons.
    /// </summary>
    public IReadOnlyList<Jaw> Jaws { get; init; } = new[]
    {
        new Jaw(new Vector2(0.22f, 0.38f), Vector2.Normalize(new Vector2(0.80f, 0.60f)), 0f, 1.00f, false),
        new Jaw(new Vector2(0.20f, 0.70f), Vector2.Normalize(new Vector2(0.85f, -0.50f)), 15f, 0.85f, false),
        new Jaw(new Vector2(0.62f, 0.44f), Vector2.Normalize(new Vector2(-0.55f, 0.85f)), 25f, 0.90f, true),
    };

    /// <summary>The rows of the silhouette searched for the body's front-most point, which places the front jaws' x.</summary>
    public float ProbeFrom { get; init; } = 0.30f;

    /// <inheritdoc cref="ProbeFrom"/>
    public float ProbeTo { get; init; } = 0.80f;

    /// <summary>How far inside the silhouette's front edge the front jaws bite (share of the body's width).</summary>
    public float ProbeInset { get; init; } = 0.08f;

    // ── TIMING (ms after a jaw's own start; the phrase is fast in, readable hit, fast out) ──────

    /// <summary>The jaw spawns this far from its bite point (a share of its own height, plus a few px) and darts in.</summary>
    public float SpawnDistShare { get; init; } = 0.45f;

    /// <inheritdoc cref="SpawnDistShare"/>
    public float SpawnDistPx { get; init; } = 8f;

    /// <summary>The dart: from the spawn to the bite point, mouth open.</summary>
    public float DartMs { get; init; } = 16f;

    /// <summary>The CHOMP: the mouth shuts (the SHUT state), with a one-frame squash.</summary>
    public float ChompAtMs { get; init; } = 20f;

    /// <inheritdoc cref="ChompAtMs"/>
    public float SquashMs { get; init; } = 16f;

    /// <summary>After the chomp the head recoils outward this far (px) over this long, then dissolves.</summary>
    public float RecoilPx { get; init; } = 4f;

    /// <inheritdoc cref="RecoilPx"/>
    public float RecoilMs { get; init; } = 24f;

    /// <summary>The dissolve: from here the head fades (drifting a little further out), gone by <see cref="GoneMs"/>.</summary>
    public float DissolveFromMs { get; init; } = 55f;

    /// <inheritdoc cref="DissolveFromMs"/>
    public float GoneMs { get; init; } = 100f;

    /// <summary>The answer lands on the MAIN chomp (the first jaw's): the number, the flash, a kill's fall.</summary>
    public float AnswerAtMs { get; init; } = 25f;

    /// <summary>The target-local Shadow residue at each bite point: fading from the chomp, gone by then (ms after the first frame).</summary>
    public float ResidueGoneMs { get; init; } = 120f;

    /// <summary>The residue's strength and its length as a share of the jaw's height.</summary>
    public float ResiduePeak { get; init; } = 0.55f;

    /// <inheritdoc cref="ResiduePeak"/>
    public float ResidueLengthShare { get; init; } = 0.9f;

    /// <summary>The whole phrase's life: the last jaw's delay plus its gone, and the residue, whichever is later.</summary>
    public float EndMs
    {
        get
        {
            var last = 0f;
            for (var i = 0; i < Jaws.Count; i++) last = Math.Max(last, Jaws[i].DelayMs + GoneMs);   // indexed: a foreach over the interface boxed an enumerator every frame
            return Math.Max(last, ResidueGoneMs);
        }
    }

    // ── LIGHT (the Source) ───────────────────────────────────────────────────────────────────────

    /// <summary>The chomp's glint: a small Shadow flare at the bite point, its peak and its life (ms).</summary>
    public float GlintPeak { get; init; } = 0.55f;

    /// <inheritdoc cref="GlintPeak"/>
    public float GlintMs { get; init; } = 45f;

    /// <summary>The glint's size as a share of the jaw's height.</summary>
    public float GlintShare { get; init; } = 0.5f;

    /// <summary>While the champion performs an action, the reaction's light plays at this share: it stays secondary.</summary>
    public float LightUnderAction { get; init; } = 0.55f;

    // ── SOUND, FLASH AND THE SCREEN'S GENERIC CUES ───────────────────────────────────────────────

    /// <summary>
    /// The reaction's ONE cue for the whole three-bite phrase, most specific first: a small supernatural CHOMP with two
    /// very quiet secondary ticks baked in behind it (never three loud identical chomps). Played on the first frame.
    /// </summary>
    public IReadOnlyList<string> SnapCues { get; init; } = new[] { "sfx_seeker_jaws_chomp", "sfx_hit" };

    /// <inheritdoc cref="SnapCues"/>
    public float SnapVolume { get; init; } = 0.40f;

    /// <summary>How wide the cue is panned toward its target (± at the arena's edges).</summary>
    public float PanWidth { get; init; } = 0.35f;

    /// <summary>
    /// The per-trigger callout ("SNARE"). Off: a reaction fires every few seconds, and the jaws, the number ("−4 JAWS")
    /// and the dock already say what happened (RH_REACTION_CALLOUT=1 restores it, for a comparison film).
    /// </summary>
    public bool Callout { get; init; }

    /// <summary>
    /// The bitten creature's flash for the reflected blow (on the main chomp): the generic F2 scale, ~0.45 for ~80 ms,
    /// never a white-out. The jaws stay visible over it.
    /// </summary>
    public float TargetFlash { get; init; } = 0.45f;

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

    /// <summary>RH_REACTION_SIZE=&lt;share&gt;: the jaws' size against the recipe's (a size comparison film). 1 otherwise.</summary>
    public static readonly float SizeDial =
        float.TryParse(Environment.GetEnvironmentVariable("RH_REACTION_SIZE"), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0f ? s : 1f;

    /// <summary>THE SEEKER's JAWS (the REACTION reference): Shadow piranha, bite for bite.</summary>
    public static readonly ReactionRecipe SeekerJaws = new() { Id = "seeker.jaws" };

    // THE LOOKUP (the accepted contract: skill-specific → deliberately shared → legacy). A reaction's presentation
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
