using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// How much of the screen an action may take (ADR-011). Gameplay impact governs visual impact: an ordinary
/// repeated hit may never outshine a signature or a boss event.
/// </summary>
public enum ActionWeight
{
    /// <summary>Idle detail, a passive state.</summary>
    Quiet,
    /// <summary>A basic attack, a common tick.</summary>
    Ordinary,
    /// <summary>An equipped active skill.</summary>
    Skill,
    /// <summary>The champion's defining action.</summary>
    Signature,
    /// <summary>A boss, a critical, a rare event.</summary>
    Major,
}

/// <summary>
/// What every authored action's recipe says (ADR-011), whatever it does between its anchor and its contact: the
/// clip it performs, the marker its schedule is anchored on and how long before the beat that marker falls, its
/// sounds, its mix, its weight and the enemy's reaction. A projectile (<see cref="ProjectileActionRecipe"/>) and a
/// melee strike (<see cref="MeleeActionRecipe"/>) are two shapes of it.
/// </summary>
public interface IActionRecipe
{
    /// <summary>A name for the recipe, for traces and evidence.</summary>
    string Id { get; }

    /// <summary>
    /// The authored clip this recipe plays: the champion's strip <c>char_&lt;id&gt;_&lt;clip&gt;_strip8_512</c> with
    /// its timing file beside it. A Form recipe plays the Form's clip (SPRAY: <c>projectile</c>). A skill's own recipe
    /// names its own clip (HARD HANDS: <c>hard_hands</c>), so the Form's shared strip stays the other skills' (BLOW
    /// still swings <c>strike</c>). When that strip is absent the skill plays its Form's plain clip, as before.
    /// </summary>
    string ClipKey { get; }

    /// <summary>
    /// For a FORM recipe (<see cref="ActionRecipes"/>' second tier), the skill effects (the skill's FxKey) it
    /// performs, or null for every skill of its Form. A skill's own recipe ignores it.
    /// </summary>
    IReadOnlyCollection<string>? EffectKeys { get; }

    /// <summary>The clip marker the schedule anchors: it falls <see cref="TravelMs"/> before the beat.</summary>
    string ReleaseMarker { get; }

    /// <summary>The socket on the clip's frames where the action leaves the body (the throwing hand, the fist).</summary>
    string HandSocket { get; }

    /// <summary>How long before the beat the anchor marker falls: a projectile's flight, 0 for a melee blow.</summary>
    float TravelMs { get; }

    /// <summary>The sound at the release (a throw) or the commit (a lunge), most specific first.</summary>
    IReadOnlyList<string> ReleaseCues { get; }
    float ReleaseVolume { get; }
    float ReleasePitch { get; }

    /// <summary>The ONE contact sound, most specific first.</summary>
    IReadOnlyList<string> ContactCues { get; }
    float ContactVolume { get; }
    float ContactPitch { get; }

    /// <summary>The contact's quiet secondary ticks (a wide fan), how many, how loud and how far apart.</summary>
    IReadOnlyList<string> ContactTickCues { get; }
    int ContactTicks { get; }
    float ContactTickVolume { get; }
    float ContactTickSpacingMs { get; }

    /// <summary>How far a sound pans at the arena's edge.</summary>
    float PanWidth { get; }

    /// <summary>Every other one-shot's level from the release to <see cref="DuckTailMs"/> after the contact.</summary>
    float DuckOthers { get; }
    float DuckTailMs { get; }

    /// <summary>The action's weight on screen.</summary>
    ActionWeight Weight { get; }

    /// <summary>The struck enemy's flash: strength, life and rise (see <see cref="ProjectileActionRecipe.TargetFlashMs"/>).</summary>
    float TargetFlash { get; }
    float TargetFlashMs { get; }
    float TargetFlashRise { get; }

    /// <summary>The generic hit puff and hit sound give way to this action's own contact.</summary>
    bool ReplacesGenericHit { get; }

    /// <summary>The skill's name is announced at the release (presentation only), not at the beat.</summary>
    bool CalloutAtRelease { get; }
}

/// <summary>
/// ONE AUTHORED PROJECTILE ACTION (ADR-011): the recipe a performance plays so that anticipation, release,
/// travel, contact and recovery read as one action — with the CONTACT on the fight's beat.
/// </summary>
/// <remarks>
/// <para>
/// The fight resolves the hit at the beat and the screen used to show everything there, including the
/// knife's launch; the knife then arrived 683 ms after its own damage number. Now the beat is the contact:
/// the clip starts early enough that its <see cref="ReleaseMarker"/> lands <see cref="TravelMs"/> before the
/// beat, the blades fly that long, and the fight's own hit feedback — flash, number, health, death — lands on
/// the frame they arrive. Nothing about the fight's outcome or its timing moves.
/// </para>
/// <para>
/// A recipe is data: which clip it performs, which socket the throw leaves from, the object thrown (the SAME
/// texture held in the hand and flying), how it travels, what it sounds like, how heavy it is. The Seeker's
/// SPRAY is one instance; nothing in the performance knows it is the Seeker.
/// </para>
/// </remarks>
public sealed record ProjectileActionRecipe : IActionRecipe
{
    /// <summary>A name for the recipe, for traces and evidence.</summary>
    public required string Id { get; init; }

    /// <summary>The Form clip this recipe performs (the skill's ClipKey).</summary>
    public required string ClipKey { get; init; }

    /// <inheritdoc/>
    public IReadOnlyCollection<string>? EffectKeys { get; init; }

    /// <summary>The clip marker at which the thrown objects leave the hand.</summary>
    public string ReleaseMarker { get; init; } = "release";

    /// <summary>The socket the throw leaves from, authored on the clip's frames.</summary>
    public string HandSocket { get; init; } = "ThrowHand";

    /// <summary>The thrown object's MATERIAL texture: drawn as it is, never tinted (pointing along +X).</summary>
    public required string PropKey { get; init; }

    /// <summary>The object's emissive mask (its edge and tip): drawn as light in the Source colour.</summary>
    public required string PropEdgeKey { get; init; }

    /// <summary>Where the hand holds the prop, in its texture pixels: the pivot it turns about.</summary>
    public Vector2 PropPivot { get; init; }

    /// <summary>How many copies the hand shows as a bundle before the release (not the number thrown).</summary>
    public int PropBundle { get; init; } = 3;

    /// <summary>How far the bundle's outer copies fan from the socket's direction, in degrees.</summary>
    public float PropFanDegrees { get; init; } = 13f;

    /// <summary>How long the thrown objects fly, in ms: the time from the release to the beat.</summary>
    public float TravelMs { get; init; } = 250f;

    /// <summary>
    /// The departure shaping of the flight: 0 = constant speed, 0.06 = leaves 6 % faster than it arrives
    /// on average. Kept small: a thrown blade keeps its momentum through contact.
    /// </summary>
    public float Departure { get; init; } = 0.06f;

    /// <summary>How far the outermost blade bows away from its straight line at mid-flight, as a share of the caster's height.</summary>
    public float FanBulge { get; init; } = 0.05f;

    /// <summary>The look of each flying object: its trail, glint, sparks and impact (see <see cref="ProjectileLook"/>).</summary>
    public required ProjectileLook Look { get; init; }

    /// <summary>Where on the target body the object lands: fractions of its visible rectangle.</summary>
    public Vector2 ContactPoint { get; init; } = new(0.34f, 0.46f);

    /// <summary>The release sound, most specific first; the first that exists plays.</summary>
    public IReadOnlyList<string> ReleaseCues { get; init; } = Array.Empty<string>();

    /// <summary>The release sound's volume and pitch (octaves).</summary>
    public float ReleaseVolume { get; init; } = 0.3f;
    public float ReleasePitch { get; init; }

    /// <summary>The contact sound, most specific first. It plays ONCE for all the blades that land together.</summary>
    public IReadOnlyList<string> ContactCues { get; init; } = Array.Empty<string>();

    /// <summary>The contact sound's volume and pitch (octaves).</summary>
    public float ContactVolume { get; init; } = 0.34f;
    public float ContactPitch { get; init; }

    /// <summary>
    /// The contact's SECONDARY voice: quiet ticks for the outermost targets of a wide fan, so four or five blades
    /// landing together are heard as one contact with width — never as four or five equal impacts.
    /// </summary>
    public IReadOnlyList<string> ContactTickCues { get; init; } = Array.Empty<string>();

    /// <summary>At most this many ticks (for fans of three or more), and their volume.</summary>
    public int ContactTicks { get; init; }
    public float ContactTickVolume { get; init; } = 0.2f;

    /// <summary>How far apart the ticks fall after the main contact, in ms: a spread the ear reads as width.</summary>
    public float ContactTickSpacingMs { get; init; } = 18f;

    /// <summary>How far a sound pans at the arena's edge (0 = centred, 1 = hard). Kept small on purpose.</summary>
    public float PanWidth { get; init; } = 0.3f;

    /// <summary>
    /// THE MIX WHILE THE ACTION SPEAKS: every OTHER one-shot (an enemy's bite, another skill's cast, a death) plays
    /// at this share of its volume from the release until <see cref="DuckTailMs"/> after the contact, so the
    /// release, the silent flight and the contact are heard as one phrase. A duck, never a mute: those sounds are
    /// still combat information. 1 = no duck.
    /// </summary>
    public float DuckOthers { get; init; } = 1f;

    /// <summary>How long after the contact the other sounds stay ducked, in ms: the contact's own ring.</summary>
    public float DuckTailMs { get; init; } = 160f;

    /// <summary>The action's weight on screen.</summary>
    public ActionWeight Weight { get; init; } = ActionWeight.Skill;

    /// <summary>
    /// How strong the struck enemy's white flash is, as a share of the fight's usual one. A fan that lands on
    /// the whole pack at once turned every creature into a solid white silhouette for ~120 ms, which buried the
    /// blades' own contact; the reaction still reads at half strength.
    /// </summary>
    public float TargetFlash { get; init; } = 1f;

    /// <summary>
    /// The struck enemy's flash ENVELOPE: how long it lives (ms) and what share of that it spends rising. The fight's
    /// usual flash swells over its first fifth of 200 ms, so a SWING's white arrives inside the blow. A thrown blade
    /// has already arrived on its contact frame: its flash peaks there (rise 0) and is gone sooner. The swell put
    /// the pack's peak 40 ms after the knives and held it grey for 200 ms, the largest change on screen after the hit.
    /// </summary>
    public float TargetFlashMs { get; init; } = 200f;
    public float TargetFlashRise { get; init; } = 0.2f;

    /// <summary>The generic hit puff and hit sound give way to this action's own contact, which describes the same blow.</summary>
    public bool ReplacesGenericHit { get; init; } = true;

    /// <summary>The skill's name is announced at the release (presentation only), not at the beat.</summary>
    public bool CalloutAtRelease { get; init; } = true;

    /// <summary>
    /// How long the release accent — the smear the hand leaves as it arrives at the release — lasts, in ms. It
    /// appears ON the release frame (never before: the hand is not there yet) and fades out over this time.
    /// </summary>
    public float SmearMs { get; init; } = 90f;

    /// <summary>
    /// How much of the hand's path the accent shows, as the share nearest the release (0.18 = the last 18%). The
    /// whole path from the coil crosses the head; only its end belongs to the throw.
    /// </summary>
    public float SmearTail { get; init; } = 0.18f;

    /// <summary>How high the hand's path arcs over the chord from the coil to the release, as a share of its length.</summary>
    public float SmearLift { get; init; } = 0.5f;
}

/// <summary>
/// How a melee contact LOOKS (ADR-011): a compact impact that begins at the point of contact and follows the force.
/// Every size is a share of the caster's visible height; every light layer takes the cast's Source colour.
/// </summary>
public sealed record MeleeImpactLook
{
    /// <summary>
    /// The white-hot flash at the contact point (a part texture), its size across the force and life. It is
    /// COMPRESSED: <see cref="FlashSquash"/> is its depth along the force against its width across it, so the first
    /// frame of the hit reads as a blow flattening into what it struck, not as a round glow. The first pass (a round
    /// 0.2 flash) read as a small pink dot at play size.
    /// </summary>
    public string FlashKey { get; init; } = "fxp_flash_soft";
    public float FlashSize { get; init; } = 0.36f;
    public float FlashSquash { get; init; } = 0.5f;
    public float FlashSeconds { get; init; } = 0.09f;

    /// <summary>
    /// The COMPRESSION RING: a shock front across the force, squashed along it (<see cref="RingSquash"/> is its
    /// thickness along the force against its width across it), growing from <see cref="RingFrom"/> to
    /// <see cref="RingTo"/> and gone in <see cref="RingSeconds"/>.
    /// </summary>
    public string RingKey { get; init; } = "fxp_ring_soft";
    public float RingFrom { get; init; } = 0.1f;
    public float RingTo { get; init; } = 0.5f;
    public float RingSquash { get; init; } = 0.45f;
    public float RingSeconds { get; init; } = 0.11f;

    /// <summary>
    /// DEBRIS thrown mostly along the force: dark fragments of what was struck (material, alpha-blended), light
    /// chips (the same slivers as light, the Source toward white by <see cref="ChipWhite"/>: SPRAY's impact
    /// draws its debris as light, and dark matter alone vanished against shadow creatures on a dark floor) and a few
    /// sparks. Spread is the half-angle about the force, reach how far the fastest one flies; a share of the chips
    /// (<see cref="Rebound"/>) kicks back off the blow instead.
    /// </summary>
    public string ShardKey { get; init; } = "fxp_shard_sliver";
    public int Shards { get; init; } = 5;
    public int Chips { get; init; } = 6;
    public float ChipWhite { get; init; } = 0.55f;
    public float Rebound { get; init; } = 0.34f;
    public string SparkKey { get; init; } = "fxp_spark_dot";
    public int Sparks { get; init; } = 6;
    public float SpreadDegrees { get; init; } = 62f;
    public float Reach { get; init; } = 0.34f;
    public float DebrisSeconds { get; init; } = 0.22f;
    public float ShardLength { get; init; } = 0.07f;
    public float Gravity { get; init; } = 2.6f;

    /// <summary>The fragments' own colour: the struck creatures are shadow, so their matter is a near-black violet.</summary>
    public Color ShardColour { get; init; } = new(46, 34, 58);

    /// <summary>A second target of the same blow (a VOLLEY) gets the ring only, at this share of the size.</summary>
    public float SecondaryScale { get; init; } = 0.6f;
}

/// <summary>
/// ONE AUTHORED MELEE ACTION (ADR-011): anticipation, commit, CONTACT ON THE BEAT, follow-through, recovery; the
/// body carried to its target and back by presentation root motion; an impact that begins where the blow lands.
/// </summary>
/// <remarks>
/// <para>
/// A melee blow has no flight: its CONTACT marker is its anchor and falls on the beat itself
/// (<see cref="TravelMs"/> = 0), so everything that describes the hit — the fist arriving, the impact, the flash, the
/// health, the number, the sound — converges on one frame.
/// </para>
/// <para>
/// The champion stands hundreds of pixels from the creatures, so the blow cannot happen where he stands. The recipe
/// LUNGES him there: a small pull-back during the anticipation, the whole distance during the commit (accelerating
/// into the hit), a small overshoot in the follow-through, and a controlled return during the recovery. The distance
/// is measured at the start of the clip from the fist's authored socket on the contact frame to the target, so the
/// fist arrives where the creature actually is. The fight's positions never move: this is presentation only.
/// </para>
/// </remarks>
public sealed record MeleeActionRecipe : IActionRecipe
{
    /// <inheritdoc/>
    public required string Id { get; init; }

    /// <inheritdoc/>
    public required string ClipKey { get; init; }

    /// <inheritdoc/>
    public IReadOnlyCollection<string>? EffectKeys { get; init; }

    /// <summary>The frame the blow lands on: the schedule's anchor, on the beat.</summary>
    public string ContactMarker { get; init; } = "contact";

    /// <summary>The frame the body launches on (the lunge, the commit sound, the callout).</summary>
    public string CommitMarker { get; init; } = "commit";

    /// <summary>The frame the recovery begins on (the return starts there).</summary>
    public string RecoveryMarker { get; init; } = "recovery";

    /// <inheritdoc/>
    string IActionRecipe.ReleaseMarker => ContactMarker;

    /// <inheritdoc/>
    public string HandSocket { get; init; } = "StrikeHand";

    /// <inheritdoc/>
    float IActionRecipe.TravelMs => 0f;

    /// <summary>Where on the target body the blow lands: fractions of its visible rectangle.</summary>
    public Vector2 ContactPoint { get; init; } = new(0.3f, 0.32f);

    /// <summary>The anticipation's pull-back and the follow-through's overshoot, as shares of the caster's height.</summary>
    public float LungeBack { get; init; } = 0.04f;
    public float LungeOvershoot { get; init; } = 0.03f;

    /// <summary>How sharply the commit accelerates into the hit (1 = constant speed, higher = later and faster).</summary>
    public float LungeAccel { get; init; } = 1.35f;

    /// <summary>
    /// The share of the recovery the RETREAT takes, from its start: the body travels home in the recovery's retreat
    /// pose and has almost arrived when the exit pose shows, which settles the last few pixels (1 = the whole
    /// recovery). At normal TEMPO that is ~250 ms: a heavy blow's controlled withdrawal. It was 0.62 of a shorter
    /// recovery (~136 ms, a 25 px hop), which read as a spring back rather than a body recovering.
    /// </summary>
    public float ReturnShare { get; init; } = 0.76f;

    /// <summary>
    /// How slowly the retreat LEAVES the target: its spacing is a smoothstep raised to this power, so it starts slow
    /// (the blow spent the body's forward momentum), is fastest mid-way and settles into home. 1 = symmetric.
    /// </summary>
    public float ReturnEase { get; init; } = 1.3f;

    /// <summary>
    /// The shortest the way home may take, in ms, however hard a handoff compresses the recovery: a 500 px return in
    /// one or two frames is a teleport. Past the clip's end the return is finished by the performance itself, under
    /// whatever the figure plays next.
    /// </summary>
    public float MinReturnMs { get; init; } = 110f;

    /// <summary>
    /// The retreat's LOW BOUND: the body's highest lift (a share of the caster's height), only through the middle of
    /// the retreat (<see cref="BoundFrom"/> to <see cref="BoundTo"/> of it): it pushes off, travels low, and has landed
    /// before the exit pose shows. The first cut was a 0.06 hop over the whole return, an airborne spring back.
    /// </summary>
    public float HopHeight { get; init; } = 0.018f;
    public float BoundFrom { get; init; } = 0.15f;
    public float BoundTo { get; init; } = 0.85f;

    /// <summary>
    /// The SPEED LINES behind a lunging body (the commit and the return): how many, their length against the
    /// distance covered in the last <see cref="SpeedLineMs"/>, and their brightness. Zero lines = none.
    /// </summary>
    public int SpeedLines { get; init; } = 3;
    public float SpeedLineMs { get; init; } = 45f;
    public float SpeedLineBrightness { get; init; } = 0.3f;

    /// <summary>The contact's look.</summary>
    public MeleeImpactLook Impact { get; init; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<string> ReleaseCues { get; init; } = Array.Empty<string>();
    /// <inheritdoc/>
    public float ReleaseVolume { get; init; } = 0.3f;
    /// <inheritdoc/>
    public float ReleasePitch { get; init; }
    /// <inheritdoc/>
    public IReadOnlyList<string> ContactCues { get; init; } = Array.Empty<string>();
    /// <inheritdoc/>
    public float ContactVolume { get; init; } = 0.45f;
    /// <inheritdoc/>
    public float ContactPitch { get; init; }
    /// <inheritdoc/>
    public IReadOnlyList<string> ContactTickCues { get; init; } = Array.Empty<string>();
    /// <inheritdoc/>
    public int ContactTicks { get; init; }
    /// <inheritdoc/>
    public float ContactTickVolume { get; init; }
    /// <inheritdoc/>
    public float ContactTickSpacingMs { get; init; } = 18f;
    /// <inheritdoc/>
    public float PanWidth { get; init; } = 0.3f;
    /// <inheritdoc/>
    public float DuckOthers { get; init; } = 1f;
    /// <inheritdoc/>
    public float DuckTailMs { get; init; } = 160f;
    /// <inheritdoc/>
    public ActionWeight Weight { get; init; } = ActionWeight.Signature;
    /// <inheritdoc/>
    public float TargetFlash { get; init; } = 0.5f;
    /// <inheritdoc/>
    public float TargetFlashMs { get; init; } = 130f;
    /// <inheritdoc/>
    public float TargetFlashRise { get; init; }
    /// <inheritdoc/>
    public bool ReplacesGenericHit { get; init; } = true;
    /// <inheritdoc/>
    public bool CalloutAtRelease { get; init; } = true;
}

/// <summary>Which actions have a recipe, by champion and Form. Everything else plays exactly as it did.</summary>
public static class ActionRecipes
{
    /// <summary><c>RH_ACTION_RECIPES=0</c> plays every action the old way: a review switch for old-vs-new films.</summary>
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("RH_ACTION_RECIPES") is not ("0" or "false");

    /// <summary>
    /// THE SEEKER's SPRAY (gold-standard slice, 2026-09-24): a fan throw of his canonical throwing knife.
    /// One release, one knife per enemy the cast actually hits, all landing on the beat.
    /// </summary>
    public static readonly ProjectileActionRecipe SeekerSpray = new()
    {
        Id = "seeker.spray",
        ClipKey = "projectile",
        EffectKeys = new[] { "projectile" },      // his knife volley; WEEP (a reaction, "weep") is never it
        PropKey = "prop_seeker_throwing_knife",
        PropEdgeKey = "prop_seeker_throwing_knife_edge",
        PropPivot = new Vector2(22.5f, 12f),
        FanBulge = 0.11f,
        TargetFlash = 0.38f,
        TargetFlashMs = 130f,
        TargetFlashRise = 0f,
        Look = ProjectileLooks.SeekerThrowingKnife,
        ReleaseCues = new[] { "sfx_seeker_spray_release", "sfx_throw_release", "sfx_cast" },
        ReleaseVolume = 0.40f,
        ContactCues = new[] { "sfx_seeker_spray_hit", "sfx_blade_hit", "sfx_hit" },
        ContactVolume = 0.50f,
        ContactTickCues = new[] { "sfx_seeker_spray_tick", "sfx_blade_tick" },
        ContactTicks = 2,
        ContactTickVolume = 0.2f,
        DuckOthers = 0.45f,
        Weight = ActionWeight.Skill,
    };

    /// <summary>
    /// THE SEEKER's HARD HANDS (the melee gold standard, 2026-09-25): a leaping overhand HAMMER-FIST. He bounds in,
    /// drives his closed right fist down onto the creature with his whole weight on the beat, and retreats home. It is
    /// HARD HANDS' OWN (a signature action belongs to its skill): BLOW shares his Strike Form and its "strike" effect,
    /// and swings its own plain clip. Its own strip, <c>char_seeker_hard_hands</c>.
    /// </summary>
    public static readonly MeleeActionRecipe SeekerHardHands = new()
    {
        Id = "seeker.hard_hands",
        ClipKey = "hard_hands",
        ReleaseCues = new[] { "sfx_seeker_hard_hands_commit", "sfx_fist_commit", "sfx_cast" },
        ReleaseVolume = 0.34f,
        ContactCues = new[] { "sfx_seeker_hard_hands_hit", "sfx_fist_hit", "sfx_hit" },
        ContactVolume = 0.55f,
        DuckOthers = 0.45f,
        TargetFlash = 0.55f,
        TargetFlashMs = 120f,
        TargetFlashRise = 0f,
        Weight = ActionWeight.Signature,
    };

    // THE LOOKUP, most specific first (2026-09-25). A SIGNATURE action belongs to its SKILL: HARD HANDS' recipe once
    // resolved from the Seeker's Strike Form and "strike" effect, and BLOW, which says the same two words, was
    // performed as HARD HANDS (its lunge, its sounds, its impact, its flash, its duck, its callout timing).
    //   1. the skill's own recipe: champion + the stable SkillDef.Id;
    //   2. the skill's CHAMPION-AGNOSTIC recipe (design.md section 8): a shared skill's look and cues on any champion;
    //   3. the champion's FORM recipe, deliberately shared by every skill of that Form and effect (EffectKeys);
    //   4. none: the skill plays its Form's plain clip, as it always did.

    private static readonly Dictionary<(string Character, string Skill), IActionRecipe> BySkill = new()
    {
        [("seeker", "sig_seeker_hard_hands")] = SeekerHardHands,
    };

    // THE CHAMPION-AGNOSTIC ACTION TIER (design.md section 8), EMPTY in Phase 1: SPRAY elsewhere needs each champion's
    // own `projectile` strip timing (.clip.json) and missile, both Phase 4's; HARD HANDS is Seeker-only and never gets
    // one. PHASE 4 HAZARD: ByAnySkill["volley_spray"] would outrank the Seeker's ByForm SPRAY, so BySkill[("seeker",
    // "volley_spray")] = SeekerSpray must be added FIRST, or the Seeker's closed SPRAY resolves to the agnostic recipe.
    private static readonly Dictionary<string, IActionRecipe> ByAnySkill = new();

    /// <summary>How many champion-agnostic action recipes exist (0 in Phase 1: see the Phase 4 hazard above).</summary>
    public static int AgnosticCount => ByAnySkill.Count;

    private static readonly Dictionary<(string Character, string Clip), IActionRecipe> ByForm = new()
    {
        [("seeker", "projectile")] = SeekerSpray,
    };

    /// <summary>
    /// The recipe this champion performs for this skill, or null: that action plays as before. The skill's own recipe
    /// (<paramref name="skillId"/>, a SkillDef.Id) first; else the skill's champion-agnostic recipe (none in Phase 1);
    /// else the champion's recipe for the skill's Form (<paramref name="clipKey"/>) when it performs the skill's effect
    /// (<paramref name="effectKey"/>).
    /// </summary>
    public static IActionRecipe? For(string characterId, string skillId, string clipKey, string effectKey)
    {
        if (!Enabled) return null;
        if (BySkill.TryGetValue((characterId, skillId), out var own)) return own;
        if (ByAnySkill.TryGetValue(skillId, out var any)) return any;
        return ByForm.TryGetValue((characterId, clipKey), out var shared)
               && (shared.EffectKeys is null || shared.EffectKeys.Contains(effectKey, StringComparer.Ordinal))
            ? shared : null;
    }
}
