using System;
using System.Collections.Generic;

namespace IdleXIdle.Game.Vfx;

/// <summary>
/// How a projectile is COMPOSED at runtime (ADR-010): a head that keeps one readable size, a short hot
/// trail and a longer soft wake that follow the projectile's real flight, one travelling glint, a few shed
/// sparks, and an impact that carries the direction it came from.
/// </summary>
/// <remarks>
/// <para>
/// WHY NOT A GENERATED ANIMATION. Two pilots asked PixelLab for an eight-frame flying knife. The first came
/// back as one static picture translated across the arena; the second tumbled through ~110° and, because a
/// strip is sized by the box around all its frames, drew the knife at 40 % of its old size. The motion of
/// a projectile is mostly the motion of its TRAIL, which only the runtime knows, because only the runtime
/// knows where the projectile actually was.
/// </para>
/// <para>
/// Everything here is a share of something the fight already measures (the caster's visible height, the
/// head's length, the flight's own clock), so a look is data: an orb, a shard or a poison dart is another
/// instance of this record, not another code path. Nothing in the record says "spin".
/// </para>
/// </remarks>
public sealed record ProjectileLook
{
    /// <summary>The head: one frame, drawn pointing along +X, white (the Source tint colours it).</summary>
    public required string HeadKey { get; init; }

    /// <summary>
    /// THE BODY-SIZE CONTRACT: the head's visible LENGTH as a share of the caster's visible height. The
    /// head's own content box is measured once; rotation, glint and trail never change it.
    /// </summary>
    public required float HeadLength { get; init; }

    /// <summary>Peak orientation variation, degrees, around the true direction of travel.</summary>
    public float WobbleDegrees { get; init; } = 6f;

    /// <summary>How fast the head wobbles, in cycles per second.</summary>
    public float WobbleHz { get; init; } = 2.2f;

    /// <summary>The trail texture: soft across its height, uniform along its length.</summary>
    public required string TrailKey { get; init; }

    /// <summary>The short, bright trail, which starts at the head's rear: its lifetime in seconds.</summary>
    public float CoreSeconds { get; init; } = 0.09f;
    /// <summary>Its width at the head, as a share of the head's thickness.</summary>
    public float CoreWidth { get; init; } = 0.32f;
    /// <summary>Its opacity at the head (drawn through <see cref="VfxBlend.Light"/>, so light = opacity²).</summary>
    public float CoreBrightness { get; init; } = 1f;
    /// <summary>How far its colour is pulled from the Source tint toward white: a hot core.</summary>
    public float CoreWhite { get; init; } = 0.5f;

    /// <summary>The long soft wake behind the core: its lifetime in seconds.</summary>
    public float WakeSeconds { get; init; } = 0.30f;
    /// <summary>Its width at the head, as a share of the head's thickness.</summary>
    public float WakeWidth { get; init; } = 0.9f;
    /// <summary>Its opacity at the head.</summary>
    public float WakeBrightness { get; init; } = 0.9f;
    /// <summary>
    /// How its opacity falls with age (1 = linear; below 1 holds longer). Its LIGHT falls as the square
    /// (<see cref="VfxBlend.Light"/>), so a gentle curve here still reads as a wake that dissolves.
    /// </summary>
    public float WakeFalloff { get; init; } = 0.85f;
    /// <summary>Its lateral wave at the oldest point, as a share of the head's length.</summary>
    public float WakeWave { get; init; } = 0.035f;

    /// <summary>
    /// How long the trail survives the landing, in seconds. Short: with the head gone its front is exposed,
    /// so it tapers there and dims away rather than standing in the arena as a block of light.
    /// </summary>
    public float LandedTrailSeconds { get; init; } = 0.12f;

    /// <summary>The glint that crosses the head once: its texture.</summary>
    public required string GlintKey { get; init; }
    /// <summary>When the glint starts, as a share of the flight.</summary>
    public float GlintStart { get; init; } = 0.30f;
    /// <summary>When it ends, as a share of the flight.</summary>
    public float GlintEnd { get; init; } = 0.46f;
    /// <summary>Its size, as a share of the head's length.</summary>
    public float GlintSize { get; init; } = 0.42f;

    /// <summary>The sparks shed from the rear quarter: their texture.</summary>
    public required string SparkKey { get; init; }
    /// <summary>How many sparks one flight sheds. Small on purpose: repeated casts must not fill the arena.</summary>
    public int Sparks { get; init; } = 3;
    /// <summary>How long a spark lives, in seconds.</summary>
    public float SparkSeconds { get; init; } = 0.22f;
    /// <summary>Its size, as a share of the head's length.</summary>
    public float SparkSize { get; init; } = 0.07f;

    /// <summary>
    /// CONTACT: the flight strikes when the head's centre is this share of its length from the target's
    /// centre (0.5 = the tip touches it; 0.2 = the blade is well in). The travel curve is unchanged; this
    /// only decides the instant the head gives way to the impact, so it does not hover, decelerating, in
    /// front of the target. At 0.2 the Seeker's knife strikes at ~0.67 of its flight, where the old strip
    /// began its tail fade.
    /// </summary>
    public float ContactReach { get; init; } = 0.2f;

    /// <summary>
    /// PRE-IMPACT: how long before contact the head gains a little light and the core tightens, in seconds.
    /// Emphasis only; the head never grows.
    /// </summary>
    public float PreImpactSeconds { get; init; } = 0.08f;

    /// <summary>The impact's shard texture: drawn pointing along +X.</summary>
    public required string ShardKey { get; init; }
    /// <summary>The impact's contact flash: a soft round glow.</summary>
    public required string FlashKey { get; init; }
    /// <summary>How many shards the impact throws.</summary>
    public int ImpactShards { get; init; } = 8;
    /// <summary>The share of them thrown forward, in the cone the projectile came in on. The rest go radially.</summary>
    public float ImpactForward { get; init; } = 0.75f;
    /// <summary>The forward cone's half-angle, degrees.</summary>
    public float ImpactSpread { get; init; } = 32f;
    /// <summary>How far a forward shard travels, as a share of the head's length.</summary>
    public float ImpactReach { get; init; } = 0.55f;
    /// <summary>How long a shard lives, in seconds.</summary>
    public float ImpactSeconds { get; init; } = 0.26f;
    /// <summary>A shard's length, as a share of the head's length.</summary>
    public float ShardLength { get; init; } = 0.16f;
    /// <summary>A shard's thickness, as a share of its own length: slim, so a spray reads as slivers, not teeth.</summary>
    public float ShardThickness { get; init; } = 0.28f;
    /// <summary>How far a shard's colour is pulled from the Source tint toward white: warm, not a white spray.</summary>
    public float ShardWhite { get; init; } = 0.2f;
    /// <summary>The contact flash's size, as a share of the head's length.</summary>
    public float FlashSize { get; init; } = 0.45f;
    /// <summary>How long the flash lives, in seconds: a hot instant, not a glow.</summary>
    public float FlashSeconds { get; init; } = 0.09f;

    /// <summary>
    /// Positional afterimages behind the head (0 = none). Optional; the trail usually carries continuity.
    /// Evaluated for the Seeker's knife at 2 (35 % and 16 %): the ghosts overlap a long blade and smear its
    /// silhouette, so the knife uses none.
    /// </summary>
    public int AfterImages { get; init; }
}

/// <summary>
/// Which strips are drawn as a composite projectile instead of as a strip. Keyed by the strip key
/// <c>HuntScreen.FxFor</c> resolves, so the lookup contract does not change: only this table decides.
/// </summary>
public static class ProjectileLooks
{
    /// <summary>THE SEEKER's thrown knife (pilot, 2026-09-23): stable forward blade, glint, sharp trail.</summary>
    public static readonly ProjectileLook SeekerKnife = new()
    {
        HeadKey = "fxp_seeker_knife_head",
        HeadLength = 0.68f,
        TrailKey = "fxp_trail_soft",
        GlintKey = "fxp_glint_star",
        SparkKey = "fxp_spark_dot",
        ShardKey = "fxp_shard_sliver",
        FlashKey = "fxp_flash_soft",
    };

    private static readonly Dictionary<string, ProjectileLook> ByStrip = new(StringComparer.Ordinal)
    {
        ["fx_seeker_projectile_strip8_512"] = SeekerKnife,
    };

    /// <summary>The composite look for this strip key, or null: that strip draws as a strip.</summary>
    public static ProjectileLook? For(string stripKey) => ByStrip.GetValueOrDefault(stripKey);

    /// <summary>Every strip key that is drawn as a composite, and its look.</summary>
    public static IReadOnlyDictionary<string, ProjectileLook> All => ByStrip;
}
