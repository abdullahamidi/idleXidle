using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Builds;

namespace IdleXIdle.Game.Vfx;

/// <summary>
/// Every effect the fight can draw, as authored placement data.
/// </summary>
/// <remarks>
/// <para>
/// This table replaces the eighteen hand-tuned spawn expressions that used to live inside
/// <c>HuntScreen</c> — <c>ChampBox.Center.Y + 40</c>, <c>ChampBox.Bottom - 156</c>,
/// <c>EnemyScale(slot, 1.3f)</c>, <c>Math.Clamp((tx - ChampBox.Right) / 104, 2, 5)</c>. Two of those
/// were integer BUCKETS (five sizes for every creature in the game, four for a bolt), one was a
/// documented "hand-tuned so the effect's FOOT sits on the ground line", and every champion-side one
/// aimed at a rectangle the champion was not standing in.
/// </para>
/// <para>
/// The sizes come from the brief's §65 bands, measured against the subject's VISIBLE height:
/// a barrier 1.10–1.20x, an ordinary impact 0.25–0.40x, an execute 0.50–0.70x, a body aura 1.0–1.2x.
/// Because <c>UiKit.AnimSprite</c> normalises every champion to the same drawn height, a
/// height-based scale is identical on all ten hunters by construction — which is also why testing
/// only THE SEEKER and THE MAGPIE proves nothing about the contract (they differ by 13 % in width;
/// the real spread is THE OATHBOUND's 162 px against QUIVER's 373 px).
/// </para>
/// <para>
/// <b>Every asset key here is a literal in ONE table.</b> That is deliberate: the gate that was
/// supposed to catch a missing effect (tools/check_asset_keys.py) reads string literals at call
/// sites, and six spawn sites passed a computed key, so PRESS, WEEP and WILT named art nothing could
/// resolve and simply drew nothing for months. With the keys in one table the gate has something to
/// read, and <c>vfx_asset_test</c> walks the rest.
/// </para>
/// </remarks>
public static class VfxProfiles
{
    // ── Impacts ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>The small puff on every other blow. Was five integer size buckets; now a ratio.</summary>
    public static readonly VfxProfile ImpactWeak = new(
        "impact.weak", "fx_weakhit", VfxSubjectKind.Creature, VfxAnchor.Center, 0.32f, Fps: 16f);

    /// <summary>The creature's bite landing on the champion. Follows his lunge now, because it is pinned to him.</summary>
    public static readonly VfxProfile ImpactBite = new(
        "impact.bite", "fx_hit", VfxSubjectKind.Champion, VfxAnchor.Center, 0.36f, Fps: 14f, OffsetY: 0.08f);

    // ── Heal / death ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The heal column. <see cref="VfxAnchor.Standing"/> IS the old hand-tuned <c>Bottom - 156</c>:
    /// the comment there said "so the effect's FOOT sits on the ground line", and that sentence is
    /// now the enum member rather than a constant that only holds at one size.
    /// </summary>
    public static readonly VfxProfile HealColumn = new(
        "heal.column", "fx_heal", VfxSubjectKind.Champion, VfxAnchor.Standing, 0.95f, Fps: 10f);

    public static readonly VfxProfile DeathChampion = new(
        "death.champion", "fx_death", VfxSubjectKind.Champion, VfxAnchor.Center, 0.80f, Fps: 9f);

    /// <summary>A boss falling: the starburst, at §65's execute band rather than at the whole body's height.</summary>
    public static readonly VfxProfile DeathBossBurst = new(
        "death.boss_burst", "fx_crit", VfxSubjectKind.Creature, VfxAnchor.Center, 0.65f, Fps: 10f, OffsetY: -0.06f);

    /// <summary>The plume, half a second after the fall — the delay is the profile's, not the caller's.</summary>
    public static readonly VfxProfile DeathCreature = new(
        "death.creature", "fx_death", VfxSubjectKind.Creature, VfxAnchor.Center, 0.70f, Fps: 9f, DelaySeconds: 0.45f);

    // ── Shield: one shell, four moments (§106) ────────────────────────────────────────────────────
    //
    //    The four share a geometry on purpose. §106 asks that the absorb "land on the barrier" and the
    //    break be "positioned correctly", and the only way to guarantee that on ten silhouettes is for
    //    all four to resolve to the same rectangle. A ripple offset onto the shell's flank was
    //    considered and rejected: the offset is normalised to the CHAMPION's width (162–373 px) while
    //    the shell is one constant size, so the same number would land somewhere different on every
    //    hunter.
    //
    //    1.15 IS THE SIZE THE ART CAN ACTUALLY DRAW. It was not reachable until 2026-09-04: fx_shield
    //    was a squat hemisphere filling 0.762 x 0.492 of its frame, so the resolver had to ask for a
    //    963-px frame to show a 474-px picture — ratio 1.88, over the §73 budget, clamped, and the
    //    barrier came out 315 px tall across a 412-px hunter's ribs. That is the "small effect centred
    //    on the torso" the brief calls a failure, and the fix was the ASSET, never the number
    //    (LAW 16). The strip is a closed ring filling its whole frame now and the same 1.15 resolves at
    //    ratio 0.93 — inside the budget, 474 px across, clearing crown and soles on all ten. The
    //    measured content boxes both sides of that change are pinned in vfx_shield_test.

    public static readonly VfxProfile ShieldBarrier = new(
        "shield.barrier", "fx_shield", VfxSubjectKind.Champion, VfxAnchor.Center, 1.15f,
        Fps: 8f, OffsetY: -0.02f, Follow: VfxFollow.Pinned, Lifetime: VfxLifetime.Held);

    public static readonly VfxProfile ShieldGain = new(
        "shield.gain", "fx_shield", VfxSubjectKind.Champion, VfxAnchor.Center, 1.15f, Fps: 12f, OffsetY: -0.02f);

    public static readonly VfxProfile ShieldAbsorb = new(
        "shield.absorb", "fx_shield", VfxSubjectKind.Champion, VfxAnchor.Center, 1.15f, Fps: 16f, OffsetY: -0.02f);

    public static readonly VfxProfile ShieldUndying = new(
        "shield.undying", "fx_shield", VfxSubjectKind.Champion, VfxAnchor.Center, 1.15f, Fps: 12f, OffsetY: -0.02f);

    /// <summary>
    /// The break bursts 20 px WIDER than the shell it replaces, so it reads as the barrier coming apart
    /// rather than as a separate picture. Its frame rate follows the motion vocabulary's REWARD band,
    /// exactly as it did before — eight frames across 0.35 s.
    /// </summary>
    public static readonly VfxProfile ShieldBreak = new(
        "shield.break", "fx_shield_break", VfxSubjectKind.Champion, VfxAnchor.Center, 1.20f,
        Fps: ShieldBreakFrames / UiMotion.Reward, OffsetY: -0.02f);

    /// <summary>How many frames the break strip holds — the art is an 8x512 row.</summary>
    public const float ShieldBreakFrames = 8f;

    // ── Casts ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The bolt. Sized to the CASTER, not to the target and not to the gap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It used to be <c>Math.Clamp((tx - ChampBox.Right) / 104, 2, 5)</c> — integer division, so the
    /// bolt had four reachable sizes and a near creature got a quarter of what a far one got for the
    /// same throw. A bolt is a thing the hunter throws: its size is a property of the throw. The gap
    /// keeps its real job, which is the travel TIME.
    /// </para>
    /// <para>
    /// Its origin is the honest version of "the hand". §63 offers a Weapon/Hand anchor conditionally —
    /// "if the current cutout rig supports a reliable semantic anchor" — and it does not: the live rig
    /// is a flipbook and the bone rig in Core has no production consumers. So the origin is stated as
    /// what it actually is, the leading edge of the silhouette at chest height, and
    /// <see cref="VfxFacing.Forward"/> makes the sign follow the figure. Today's constant
    /// <c>ChampBox.Right - 40</c> is 160 px right of centre for every hunter, which is 27 px off THE
    /// SEEKER's body — the bolt is born in empty air.
    /// </para>
    /// </remarks>
    public static readonly VfxProfile CastProjectile = new(
        "cast.projectile", "fx_projectile", VfxSubjectKind.Champion, VfxAnchor.Center, 0.28f,
        Fps: 8f, OffsetX: 0.42f, OffsetY: 0.02f, Facing: VfxFacing.Forward, Travel: VfxTravel.ToTarget);

    public static readonly VfxProfile CastAura = new(
        "cast.aura", "fx_aura", VfxSubjectKind.Champion, VfxAnchor.Center, 1.05f, Fps: 12f);

    /// <summary>
    /// The burst under the PACK. The only profile measured against a width.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A row is six hundred pixels wide and one creature tall, so its height says nothing about how big
    /// a statement about the whole pack should be. It used to be positioned by the row and SIZED by one
    /// creature — a Swarm body gave it 208 px and a boss 520 for the same cast, which is two different
    /// claims about the same skill.
    /// </para>
    /// <para>
    /// 0.80 rather than the row's full width, and STANDING rather than centred on the soles. Both are
    /// the art's doing: the strips are square bursts, so a full-width one centred underfoot reached
    /// 220 px below the ground line and across the skill dock, and only 0.80 keeps the SHARED strip
    /// inside the asset-scale budget at all. The nine per-character trap strips are still over it —
    /// see the ledger; that is an art order, not a number to turn.
    /// </para>
    /// </remarks>
    public static readonly VfxProfile CastTrap = new(
        "cast.trap", "fx_trap", VfxSubjectKind.EnemyRow, VfxAnchor.Standing, 0.80f,
        Layer: VfxLayer.GroundUnder, Fps: 12f, Basis: VfxBasis.SubjectWidth);

    /// <summary>An amplify sigil belongs over the crown, not inside the ribcage — it was drawn at 0.45 down the body.</summary>
    public static readonly VfxProfile CastMark = new(
        "cast.mark", "fx_mark", VfxSubjectKind.Creature, VfxAnchor.Head, 0.45f,
        Layer: VfxLayer.Overhead, Fps: 12f, OffsetY: -0.10f);

    public static readonly VfxProfile CastTransformation = new(
        "cast.transformation", "fx_transformation", VfxSubjectKind.Champion, VfxAnchor.Center, 1.05f, Fps: 12f);

    public static readonly VfxProfile CastStrike = new(
        "cast.strike", "fx_strike", VfxSubjectKind.Creature, VfxAnchor.Center, 0.55f, Fps: 12f);

    /// <summary>
    /// WEEP's fall, and the reason the override table exists at all.
    /// </summary>
    /// <remarks>
    /// WEEP's ClipKey is <c>projectile</c> but its art is a 0.27-wide, 0.97-tall falling column. Played
    /// through the bolt profile it would be thrown sideways across the arena. Its shape and its clip
    /// disagree, so the FxKey names the profile directly.
    /// </remarks>
    public static readonly VfxProfile CastRain = new(
        "cast.rain", "fx_weep", VfxSubjectKind.Creature, VfxAnchor.Head, 0.90f,
        Layer: VfxLayer.Overhead, Fps: 12f, OffsetY: -0.05f);

    // ── The held field ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The field a Field skill raises around the hunter — held for as long as the build carries one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ASSET is the skill's own <c>FxKey</c>, so this is the one profile whose art is chosen at
    /// spawn; <see cref="VfxProfile.AssetKey"/> carries the shared fallback. Four Field skills reach
    /// it — BRAND (mark), MIRE (aura), PRESS (press), WILT (wilt) — and until the aliases landed with
    /// this contract, two of the four resolved to nothing at all and a build running them had NO field
    /// effect whatsoever.
    /// </para>
    /// <para>
    /// <see cref="VfxLayer.BehindSubject"/> is the one deliberate visual change in the table. The
    /// designer kept seeing "ortasında küçük yanan alev" — the strip's opaque interior hazing the
    /// figure it was meant to wrap. Drawn additively BEHIND the champion the interior contributes
    /// nothing where he is opaque and glows everywhere he is not, so the field reads as a halo and the
    /// hunter stays legible.
    /// </para>
    /// <para>
    /// <see cref="VfxAnchor.Standing"/> replaces <c>ChampBox.Bottom + 18 - h/2</c>, a formula that
    /// duplicated the renderer's own sizing arithmetic minus its canvas-scale term. The field reaches
    /// the ground now instead of floating 42 px above the soles.
    /// </para>
    /// </remarks>
    public static readonly VfxProfile FieldAura = new(
        "field.aura", "fx_aura", VfxSubjectKind.Champion, VfxAnchor.Standing, 1.10f,
        Layer: VfxLayer.BehindSubject, Fps: 10f, Follow: VfxFollow.Pinned, Lifetime: VfxLifetime.Held);

    // ── The table ─────────────────────────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<VfxProfile> All = new[]
    {
        ImpactWeak, ImpactBite, HealColumn, DeathChampion, DeathBossBurst, DeathCreature,
        ShieldBarrier, ShieldGain, ShieldAbsorb, ShieldUndying, ShieldBreak,
        CastProjectile, CastAura, CastTrap, CastMark, CastTransformation, CastStrike, CastRain,
        FieldAura,
    };

    private static readonly Dictionary<string, VfxProfile> ById =
        All.ToDictionary(p => p.Id, StringComparer.Ordinal);

    public static VfxProfile Get(string id) => ById[id];

    /// <summary>
    /// Effects whose art is a different SHAPE from their clip, keyed by the skill's own FxKey.
    /// </summary>
    /// <remarks>
    /// Kept explicit and tiny. A general "shape registry" would be a second engine; one entry with a
    /// measured reason is a table.
    /// </remarks>
    private static readonly Dictionary<string, VfxProfile> FxKeyOverride = new(StringComparer.Ordinal)
    {
        ["weep"] = CastRain,
    };

    /// <summary>
    /// Which profile a skill's cast draws through: its FxKey override if it has one, else its ClipKey.
    /// </summary>
    public static VfxProfile ForSkill(SkillDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (FxKeyOverride.TryGetValue(def.FxKey, out var over)) return over;
        return def.ClipKey switch
        {
            "projectile" => CastProjectile,
            "aura" => CastAura,
            "trap" => CastTrap,
            "mark" => CastMark,
            "transformation" => CastTransformation,
            _ => CastStrike,   // "strike" and any future key
        };
    }
}
