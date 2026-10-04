using System;
using System.Collections.Generic;
using IdleXIdle.Game.Vfx;
using Microsoft.Xna.Framework;

namespace IdleXIdle.Game.Presentation;

/// <summary>How a basic attack reaches its creature (design.md 5.18-5.27).</summary>
public enum SwingKind
{
    /// <summary>A melee blow: the body STEPS IN 20-25 % of the row gap as a draw offset, never root motion.</summary>
    StepIn,

    /// <summary>A thrown missile: no step, the missile leaves the hand one flight before the beat.</summary>
    Missile,

    /// <summary>A reach (the Oathbound's chain): no step, the contact is bridged by the weapon.</summary>
    Reach,
}

/// <summary>
/// THE CONTACT PICTURE of a basic attack (T1 Ordinary, design.md section 3): drawn AT the creature on the beat, every
/// extent a share of the CREATURE's height and none over 0.35 of it, gone within 250 ms. Light (the slash, the flash, the
/// ring, the slivers, the sparks) goes through the light pass (ADR-009); the dust is a material, alpha-blended, untinted.
/// </summary>
/// <remarks>
/// A basic attack is not a skill and has no slot, so it has no Source: <see cref="Light"/> is the material's own light
/// (steel, knuckle, brass, stone, wood), pale and quiet. The Source light of design.md is the skills'.
/// </remarks>
public sealed record SwingImpactLook
{
    /// <summary>The directional slash's length (share of the creature's height); 0 draws none.</summary>
    public float SlashLength { get; init; }

    /// <summary>The slash's thickness (share of the creature's height).</summary>
    public float SlashThickness { get; init; } = 0.03f;

    /// <summary>The slash's direction on screen, degrees (0 = right, +90 = down): the blade's path through the body.</summary>
    public float SlashDegrees { get; init; } = 32f;

    /// <summary>How long the slash lives, ms: drawn along its length in the first two display frames, then fading.</summary>
    public float SlashMs { get; init; } = 140f;

    /// <summary>Thin slivers of light thrown along the slash (or the force) off the contact.</summary>
    public int Slivers { get; init; }

    /// <summary>A sliver's length (share of the creature's height).</summary>
    public float SliverLength { get; init; } = 0.06f;

    /// <summary>How far a sliver travels (share of the creature's height).</summary>
    public float SliverTravel { get; init; } = 0.16f;

    /// <summary>The flash's size across the force (share of the creature's height); 0 draws none.</summary>
    public float FlashSize { get; init; }

    /// <summary>The flash's extent ALONG the force against its size across it (below 1: compressed by the blow).</summary>
    public float FlashSquash { get; init; } = 0.5f;

    /// <summary>How long the flash lives, ms (two display frames is "for 2 frames").</summary>
    public float FlashMs { get; init; } = 90f;

    /// <summary>The flat ring's size when it is born (share of the creature's height).</summary>
    public float RingFrom { get; init; } = 0.12f;

    /// <summary>The flat ring's size when it is gone (share of the creature's height); 0 draws none.</summary>
    public float RingSize { get; init; }

    /// <summary>The ring's height against its width: FLAT (it lies on the ground plane).</summary>
    public float RingSquash { get; init; } = 0.32f;

    /// <summary>How long the ring lives, ms.</summary>
    public float RingMs { get; init; } = 170f;

    /// <summary>Hot points thrown along the force.</summary>
    public int Sparks { get; init; }

    /// <summary>How far a spark travels (share of the creature's height).</summary>
    public float SparkTravel { get; init; } = 0.2f;

    /// <summary>The dust puff's width (share of the creature's height); 0 draws none. A MATERIAL: untinted, alpha-blended.</summary>
    public float DustSize { get; init; }

    /// <summary>How long the dust lives, ms.</summary>
    public float DustMs { get; init; } = 240f;

    /// <summary>Small pieces of the missile's own material knocked off at the contact (a MATERIAL: untinted, alpha-blended).</summary>
    public int Chips { get; init; }

    /// <summary>The texture a chip is drawn with (the missile prop itself, small); none without it.</summary>
    public string? ChipKey { get; init; }

    /// <summary>A chip's size (share of the creature's height).</summary>
    public float ChipSize { get; init; } = 0.06f;

    /// <summary>How far a chip is thrown (share of the creature's height).</summary>
    public float ChipTravel { get; init; } = 0.16f;

    /// <summary>The force's direction on screen, degrees (0 = right: into the creature, which faces the champion).</summary>
    public float ForceDegrees { get; init; }

    /// <summary>The contact's own light (pre-multiplied by <see cref="Brightness"/> at draw).</summary>
    public Color Light { get; init; } = new(228, 232, 240);

    /// <summary>The light's peak strength, 0..1 (a basic attack is quiet).</summary>
    public float Brightness { get; init; } = 0.7f;

    /// <summary>The picture's largest extent, as a share of the creature's height (T1: at most 0.35).</summary>
    public float Extent => MathF.Max(MathF.Max(SlashLength, FlashSize),
                                     MathF.Max(MathF.Max(RingSize, DustSize),
                                               MathF.Max(MathF.Max(Slivers > 0 ? SliverTravel + SliverLength : 0f, Sparks > 0 ? SparkTravel : 0f),
                                                         Chips > 0 ? ChipTravel + ChipSize : 0f)));

    /// <summary>How long the whole picture lives, ms (T1: at most 250).</summary>
    public float LifeMs => MathF.Max(MathF.Max(SlashLength > 0f ? SlashMs : 0f, FlashSize > 0f ? FlashMs : 0f),
                                     MathF.Max(MathF.Max(RingSize > 0f ? RingMs : 0f, DustSize > 0f ? DustMs : 0f),
                                               Slivers > 0 || Sparks > 0 || Chips > 0 ? MathF.Max(SlashMs, 160f) : 0f));
}

/// <summary>
/// ONE CHAMPION'S BASIC ATTACK, PERFORMED (design.md 5.18-5.27, T1 Ordinary): the champion's own <c>attack</c> strip,
/// played by its authored <c>.clip.json</c> with the anchor frame ON THE BEAT, a step-in (melee), one contact cue and a
/// small contact picture at the creature. Data only: <see cref="SwingPerformance"/> plays it, <see cref="SwingClock"/>
/// times its frames.
/// </summary>
/// <remarks>
/// A SEPARATE TIER FROM <see cref="ActionRecipes"/>: a basic attack is not a SkillDef, so it is keyed by the champion
/// alone, and no skill can ever resolve to it (nor it to a skill). Ordinary everywhere: no callout, no duck, no release
/// cue for melee, the plain number.
/// </remarks>
public sealed record SwingRecipe
{
    /// <summary>A stable name for traces and tests, e.g. <c>seeker.swing</c>.</summary>
    public required string Id { get; init; }

    /// <summary>How the swing reaches its creature.</summary>
    public SwingKind Kind { get; init; } = SwingKind.StepIn;

    /// <summary>The champion clip it plays (the champion's own <c>attack</c> strip).</summary>
    public string ClipKey { get; init; } = "attack";

    /// <summary>The marker that lands on the beat less <see cref="TravelMs"/>: <c>contact</c> (a blow) or <c>release</c> (a throw).</summary>
    public string AnchorMarker { get; init; } = "contact";

    /// <summary>The marker the step-in starts from (0 before it).</summary>
    public string CommitMarker { get; init; } = "commit";

    /// <summary>The marker the step-in is home by.</summary>
    public string SettleMarker { get; init; } = "settle";

    /// <summary>The socket on the clip's frames that carries the weapon (its striking face).</summary>
    public string HandSocket { get; init; } = "StrikeHand";

    /// <summary>The step-in's peak as a share of the ROW GAP (design.md: 0.20-0.25; 0 for missiles and reaches).</summary>
    public float StepInShare { get; init; }

    /// <summary>
    /// The step-in's rise as u^power over commit..contact (2: the default ease-in). A planted, wide-stance strip that
    /// translates whole reads as a SLIDE when the move is spread over the wind-up; 3 holds the stance and lands the
    /// translation in the last ~100 ms under the thrust (the Phase 1 review: the Thornwall, the Anvil).
    /// </summary>
    public float StepInPower { get; init; } = 2f;

    /// <summary>Where on the creature's body the contact lands (fractions of its visible rect; the creature faces left).</summary>
    public Vector2 ContactPoint { get; init; } = new(0.3f, 0.45f);

    /// <summary>The contact picture.</summary>
    public required SwingImpactLook Impact { get; init; }

    /// <summary>The contact cue, most specific first (the champion's own -> the archetype -> the generic).</summary>
    public required IReadOnlyList<string> ContactCues { get; init; }

    /// <summary>The contact cue's volume (T1: 0.34-0.38).</summary>
    public float ContactVolume { get; init; } = 0.36f;

    /// <summary>The release cue (a missile's), most specific first; none for a melee blow.</summary>
    public IReadOnlyList<string> ReleaseCues { get; init; } = Array.Empty<string>();

    /// <summary>The release cue's volume (T1: 0.16-0.18).</summary>
    public float ReleaseVolume { get; init; } = 0.17f;

    /// <summary>A missile's flight, ms (its release lands this long before the beat); 0 for a blow.</summary>
    public float TravelMs { get; init; }

    /// <summary>The struck creature's flash peak (T1: 0.30).</summary>
    public float TargetFlash { get; init; } = 0.30f;

    /// <summary>The flash's length, ms (T1: 110).</summary>
    public float TargetFlashMs { get; init; } = 110f;

    /// <summary>The flash's rise (0: it is at its peak on the beat).</summary>
    public float TargetFlashRise { get; init; }

    /// <summary>The intensity tier (design.md section 3): every basic attack is Ordinary.</summary>
    public ActionWeight Weight { get; init; } = ActionWeight.Ordinary;

    /// <summary>
    /// A MISSILE's composite (ADR-010: the champion's prop as its head, the runtime trail, a glint, a directional contact);
    /// null for a blow or a reach.
    /// </summary>
    public ProjectileLook? Missile { get; init; }

    /// <summary>A missile's bow off its straight line at mid-flight, a share of the thrower's height (negative: a LOB, up).</summary>
    public float FlightBulge { get; init; }

    /// <summary>A missile's departure (<see cref="ProjectileMotion.ThrowProgress"/>): above 0 leaves fast and arrives slower.</summary>
    public float Departure { get; init; } = 0.06f;

    /// <summary>A missile's light (its edge, trail, glint and contact flash): the material's own, pale (no Source).</summary>
    public Color FlightLight { get; init; } = new(226, 230, 238);

    /// <summary>A REACH's strand (the Oathbound's chain, design.md 5.27); null for a blow or a missile.</summary>
    public ReachLook? Reach { get; init; }
}

/// <summary>
/// Which champions PERFORM their basic attack (design.md 5.18-5.27): all ten since P1.4. A champion without an entry, or whose
/// <c>char_&lt;id&gt;_attack</c> strip has no <c>.clip.json</c>, swings the old way (the plain clip, the 40 px push).
/// </summary>
public static class SwingRecipes
{
    // the steel's light: cool, pale (the Seeker's knife, the Magpie's dagger)
    private static readonly Color Steel = new(226, 234, 246);
    // a padded blow's light: warm, dull (the Anvil's fist, the Metronome's knuckles)
    private static readonly Color Knuckle = new(255, 226, 196);
    // stone and wood: warm, low (the Tower's hammer, the Thornwall's shield)
    private static readonly Color Stone = new(236, 222, 200);
    private static readonly Color Wood = new(250, 228, 190);

    /// <summary>THE SEEKER: a step-in blade cut. A thin directional slash 0.3 of the creature's height and two slivers.</summary>
    public static readonly SwingRecipe Seeker = new()
    {
        Id = "seeker.swing",
        StepInShare = 0.25f,
        Impact = new SwingImpactLook
        {
            SlashLength = 0.30f, SlashThickness = 0.028f, SlashDegrees = 34f, SlashMs = 140f,
            Slivers = 2, SliverLength = 0.06f, SliverTravel = 0.16f, ForceDegrees = 34f,
            Light = Steel, Brightness = 0.72f,
        },
        ContactCues = new[] { "sfx_seeker_swing_hit", "sfx_blade_hit", "sfx_hit" },
    };

    /// <summary>THE ANVIL: a lunge punch. A compressed flash and a small flat ring.</summary>
    public static readonly SwingRecipe Anvil = new()
    {
        Id = "anvil.swing",
        StepInShare = 0.20f,
        StepInPower = 3f,   // the planted stance holds; the lunge lands under the punch (the Phase 1 review)
        Impact = new SwingImpactLook
        {
            FlashSize = 0.28f, FlashSquash = 0.45f, FlashMs = 90f,
            RingFrom = 0.12f, RingSize = 0.30f, RingSquash = 0.32f, RingMs = 170f,
            Light = Knuckle, Brightness = 0.62f,
        },
        ContactCues = new[] { "sfx_anvil_swing_hit", "sfx_fist_hit", "sfx_hit" },
    };

    /// <summary>THE METRONOME: a running punch. A compressed flash and two sparks (FIRST BEAT is the number's, not here).</summary>
    public static readonly SwingRecipe Metronome = new()
    {
        Id = "metronome.swing",
        StepInShare = 0.25f,
        Impact = new SwingImpactLook
        {
            FlashSize = 0.24f, FlashSquash = 0.45f, FlashMs = 80f,
            Sparks = 2, SparkTravel = 0.20f,
            Light = Knuckle, Brightness = 0.66f,
        },
        ContactCues = new[] { "sfx_metronome_swing_hit", "sfx_fist_hit", "sfx_hit" },
    };

    /// <summary>THE TOWER: an overhead hammer slam that lands AT the creature. Dust and a flat ring of 0.35.</summary>
    public static readonly SwingRecipe Tower = new()
    {
        Id = "tower.swing",
        StepInShare = 0.20f,
        ContactPoint = new Vector2(0.32f, 0.72f),   // the slam comes down: low on the body
        Impact = new SwingImpactLook
        {
            RingFrom = 0.14f, RingSize = 0.35f, RingSquash = 0.28f, RingMs = 200f,
            DustSize = 0.32f, DustMs = 240f, ForceDegrees = 90f,
            Light = Stone, Brightness = 0.55f,
        },
        ContactCues = new[] { "sfx_tower_swing_hit", "sfx_stone_hit", "sfx_hit" },
    };

    /// <summary>THE THORNWALL: a shield bash. A broad short flash, 0.35 wide, for two display frames.</summary>
    public static readonly SwingRecipe Thornwall = new()
    {
        Id = "thornwall.swing",
        StepInShare = 0.20f,
        StepInPower = 3f,   // the planted stance holds; the translation lands under the shield thrust (the Phase 1 review)
        Impact = new SwingImpactLook
        {
            FlashSize = 0.35f, FlashSquash = 0.55f, FlashMs = 2000f / 60f,
            Light = Wood, Brightness = 0.66f,
        },
        ContactCues = new[] { "sfx_thornwall_swing_hit", "sfx_wood_hit", "sfx_hit" },
    };

    /// <summary>THE MAGPIE: a dagger nick, the fastest profile. A thin bright slash and one sliver.</summary>
    public static readonly SwingRecipe Magpie = new()
    {
        Id = "magpie.swing",
        StepInShare = 0.25f,
        Impact = new SwingImpactLook
        {
            SlashLength = 0.24f, SlashThickness = 0.022f, SlashDegrees = -24f, SlashMs = 100f,
            Slivers = 1, SliverLength = 0.05f, SliverTravel = 0.14f, ForceDegrees = -24f,
            Light = Steel, Brightness = 0.85f,
        },
        ContactCues = new[] { "sfx_magpie_swing_hit", "sfx_blade_hit", "sfx_hit" },
    };

    // bone, flint and iron: pale, quiet lights (the Chorus's charm, the Unbroken's chip, the Oathbound's hook)
    private static readonly Color Bone = new(238, 230, 208);
    private static readonly Color Flint = new(232, 224, 210);
    private static readonly Color Iron = new(226, 228, 238);

    /// <summary>
    /// THE QUIVER: a bow shot. No step; her arrow (<c>prop_quiver_arrow</c>) leaves the bow hand on the release frame
    /// 200 ms before the beat and lands ON it; its contact is a short forward slash carried on along its line (the
    /// arrow's look, <see cref="ProjectileLooks.QuiverArrow"/>).
    /// </summary>
    public static readonly SwingRecipe Quiver = new()
    {
        Id = "quiver.swing",
        Kind = SwingKind.Missile,
        AnchorMarker = "release",
        HandSocket = "BowHand",
        TravelMs = 200f,
        ContactPoint = new Vector2(0.3f, 0.42f),
        Missile = ProjectileLooks.QuiverArrow,
        Departure = 0.04f,
        FlightLight = Steel,
        Impact = new SwingImpactLook { Light = Steel, Brightness = 0.6f },   // the forward slash is the arrow's own contact
        ContactCues = new[] { "sfx_quiver_swing_hit", "sfx_blade_hit", "sfx_hit" },
        ReleaseCues = new[] { "sfx_quiver_loose", "sfx_throw_release" },
        ReleaseVolume = 0.18f,
    };

    /// <summary>
    /// THE CHORUS: a thrown bone charm. ONE charm leaves her hand on the release frame (the bundle her strip draws in the
    /// hand IS the charms she holds, so none is drawn there); a bone clatter at the creature: a small flash (the charm's
    /// look) and three pale slivers.
    /// </summary>
    public static readonly SwingRecipe Chorus = new()
    {
        Id = "chorus.swing",
        Kind = SwingKind.Missile,
        AnchorMarker = "release",
        HandSocket = "ThrowHand",
        TravelMs = 200f,
        ContactPoint = new Vector2(0.3f, 0.4f),
        Missile = ProjectileLooks.ChorusCharm,
        FlightBulge = -0.03f,
        FlightLight = Bone,
        Impact = new SwingImpactLook
        {
            Slivers = 3, SliverLength = 0.05f, SliverTravel = 0.14f, ForceDegrees = 0f, SlashMs = 120f,
            Light = Bone, Brightness = 0.55f,
        },
        ContactCues = new[] { "sfx_chorus_swing_hit", "sfx_wood_hit", "sfx_hit" },
        ReleaseCues = new[] { "sfx_chorus_toss", "sfx_throw_release" },
        ReleaseVolume = 0.16f,
    };

    /// <summary>
    /// THE UNBROKEN: a flung stone chip (DECIDED in design.md 5.26: a wall champion throws, he does not lunge). A short
    /// heavy LOB (220 ms, its path bowed up), a small flash (the chip's look) and two chips of the same stone knocked off.
    /// </summary>
    public static readonly SwingRecipe Unbroken = new()
    {
        Id = "unbroken.swing",
        Kind = SwingKind.Missile,
        AnchorMarker = "release",
        HandSocket = "ThrowHand",
        TravelMs = 220f,
        ContactPoint = new Vector2(0.3f, 0.45f),
        Missile = ProjectileLooks.UnbrokenChip,
        FlightBulge = -0.10f,
        Departure = 0f,
        FlightLight = Flint,
        Impact = new SwingImpactLook
        {
            Chips = 2, ChipKey = "prop_unbroken_chip", ChipSize = 0.06f, ChipTravel = 0.16f, ForceDegrees = -15f,
            Light = Flint, Brightness = 0.5f,
        },
        ContactCues = new[] { "sfx_unbroken_swing_hit", "sfx_stone_hit", "sfx_hit" },
        ReleaseCues = new[] { "sfx_unbroken_toss", "sfx_throw_release" },
        ReleaseVolume = 0.16f,
    };

    /// <summary>
    /// THE OATHBOUND: a chain lash, a REACH (design.md 5.27). No step: the chain (<c>prop_seeker_chain_body</c>, a shared
    /// iron material) leaves his lash hand over the lash frames, snaps TAUT at the creature on the beat with his hook
    /// (<c>prop_oathbound_hook</c>) at its end, and recoils over 120 ms; a spark and a small flash (0.30) at the hook.
    /// </summary>
    public static readonly SwingRecipe Oathbound = new()
    {
        Id = "oathbound.swing",
        Kind = SwingKind.Reach,
        HandSocket = "LashHand",
        ContactPoint = new Vector2(0.3f, 0.45f),
        Reach = new ReachLook
        {
            StrandKey = "prop_seeker_chain_body",
            LinkKey = "prop_seeker_chain_link",   // the strip's chunky links, not a smooth rod (the Phase 1 review)
            EndKey = "prop_oathbound_hook",
            EndEdgeKey = "prop_oathbound_hook_edge",
            EndPivot = new Vector2(10.5f, 22f),   // the hook's eye (oathbound_hook.py), where the chain runs through it
            EndEdgeBrightness = 0.3f,
            Thickness = 0.022f,
            Sag = 0.12f,
            RecoilMs = 120f,
        },
        Impact = new SwingImpactLook
        {
            FlashSize = 0.30f, FlashSquash = 0.6f, FlashMs = 80f, Sparks = 1, SparkTravel = 0.14f, ForceDegrees = 0f,
            Light = Iron, Brightness = 0.6f,
        },
        ContactCues = new[] { "sfx_oathbound_swing_hit", "sfx_fist_hit", "sfx_hit" },
    };

    private static readonly Dictionary<string, SwingRecipe> ByCharacter = new(StringComparer.Ordinal)
    {
        ["seeker"] = Seeker,
        ["anvil"] = Anvil,
        ["metronome"] = Metronome,
        ["tower"] = Tower,
        ["thornwall"] = Thornwall,
        ["magpie"] = Magpie,
        ["quiver"] = Quiver,
        ["chorus"] = Chorus,
        ["unbroken"] = Unbroken,
        ["oathbound"] = Oathbound,
    };

    /// <summary>Every champion with a swing recipe, and its recipe (for the tests and the film rig).</summary>
    public static IReadOnlyDictionary<string, SwingRecipe> All => ByCharacter;

    /// <summary>
    /// This champion's basic attack recipe, or null: it swings the old way. Gated only by <see cref="ActionRecipes.Enabled"/>
    /// (<c>RH_ACTION_RECIPES=0</c> films the legacy swing, the QA twin); no switch of its own.
    /// </summary>
    public static SwingRecipe? For(string characterId)
        => ActionRecipes.Enabled && ByCharacter.TryGetValue(characterId, out var recipe) ? recipe : null;
}
