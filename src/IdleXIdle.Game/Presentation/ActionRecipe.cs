using System;
using System.Collections.Generic;
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
public sealed record ProjectileActionRecipe
{
    /// <summary>A name for the recipe, for traces and evidence.</summary>
    public required string Id { get; init; }

    /// <summary>The Form clip this recipe performs (the skill's ClipKey).</summary>
    public required string ClipKey { get; init; }

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

    /// <summary>How far a sound pans at the arena's edge (0 = centred, 1 = hard). Kept small on purpose.</summary>
    public float PanWidth { get; init; } = 0.3f;

    /// <summary>The action's weight on screen.</summary>
    public ActionWeight Weight { get; init; } = ActionWeight.Skill;

    /// <summary>
    /// How strong the struck enemy's white flash is, as a share of the fight's usual one. A fan that lands on
    /// the whole pack at once turned every creature into a solid white silhouette for ~120 ms, which buried the
    /// blades' own contact; the reaction still reads at half strength.
    /// </summary>
    public float TargetFlash { get; init; } = 1f;

    /// <summary>The generic hit puff and hit sound give way to this action's own contact, which describes the same blow.</summary>
    public bool ReplacesGenericHit { get; init; } = true;

    /// <summary>The skill's name is announced at the release (presentation only), not at the beat.</summary>
    public bool CalloutAtRelease { get; init; } = true;

    /// <summary>How long the release arc — the smear the hand leaves between the coil and the release — lasts, in ms.</summary>
    public float SmearMs { get; init; } = 90f;
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
        PropKey = "prop_seeker_throwing_knife",
        PropEdgeKey = "prop_seeker_throwing_knife_edge",
        PropPivot = new Vector2(22.5f, 12f),
        FanBulge = 0.11f,
        TargetFlash = 0.38f,
        Look = ProjectileLooks.SeekerThrowingKnife,
        ReleaseCues = new[] { "sfx_seeker_spray_release", "sfx_throw_release", "sfx_cast" },
        ReleaseVolume = 0.24f,
        ReleasePitch = 0.45f,
        ContactCues = new[] { "sfx_seeker_spray_hit", "sfx_blade_hit", "sfx_hit" },
        ContactVolume = 0.36f,
        ContactPitch = 0.18f,
        Weight = ActionWeight.Skill,
    };

    private static readonly Dictionary<(string Character, string Clip), ProjectileActionRecipe> ByActor = new()
    {
        [("seeker", "projectile")] = SeekerSpray,
    };

    /// <summary>The recipe this champion performs for this Form, or null: that action plays as before.</summary>
    public static ProjectileActionRecipe? For(string characterId, string clipKey)
        => Enabled && ByActor.TryGetValue((characterId, clipKey), out var r) ? r : null;

    /// <summary>Every recipe, keyed by champion and Form.</summary>
    public static IReadOnlyDictionary<(string Character, string Clip), ProjectileActionRecipe> All => ByActor;
}
