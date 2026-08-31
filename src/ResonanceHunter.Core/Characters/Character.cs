using System;
using System.Collections.Generic;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;

namespace ResonanceHunter.Core.Characters;

/// <summary>How a character is earned.</summary>
/// <remarks>
/// The kind was declared before quests existed because the alternative — bolting a second unlock
/// mechanism on later — is how a system ends up with two of everything, and this codebase has been
/// bitten by parallel systems more than once. Quests exist now (<see cref="Quests.QuestCatalogue"/>);
/// the host evaluates them each frame and calls <see cref="CharacterState.CompleteQuest"/>.
/// </remarks>
public enum UnlockKind
{
    /// <summary>Yours from the first session.</summary>
    Start,

    /// <summary>Earned by conquering a region — see <see cref="CharacterUnlock.RegionId"/>.</summary>
    Conquest,

    /// <summary>Earned by finishing a quest — see <see cref="CharacterUnlock.QuestId"/>.</summary>
    Quest,
}

/// <summary>Which of a class's two champions this is — the one you earn early, or the one you work for.</summary>
/// <remarks>
/// Playtest (2026-08-26): "All the characters unlock far too easily. The first character of each class
/// may unlock somewhat easily, but the second one should take effort." So every class has exactly one
/// FIRST, earned by conquest (or given at the start), and exactly one SECOND, earned by a quest with a
/// real demand behind it. The tier is data on the roster rather than a rule in a screen, so a test can
/// hold the roster to it and the card can say which one it is looking at.
/// </remarks>
public enum ClassTier
{
    /// <summary>The champion a class hands you early — a conquest gate, or the starter.</summary>
    First,

    /// <summary>The champion a class makes you earn — always a quest gate.</summary>
    Second,
}

/// <summary>What a character costs to have.</summary>
public sealed record CharacterUnlock
{
    public static readonly CharacterUnlock Start = new() { Kind = UnlockKind.Start };

    public static CharacterUnlock Conquest(string regionId) =>
        new() { Kind = UnlockKind.Conquest, RegionId = regionId };

    public static CharacterUnlock Quest(string questId, string describe) =>
        new() { Kind = UnlockKind.Quest, QuestId = questId, QuestText = describe };

    public UnlockKind Kind { get; init; }
    public string? RegionId { get; init; }
    public string? QuestId { get; init; }

    /// <summary>
    /// Player-facing text for a quest gate. The screens prefer the quest's own <c>Demand</c>; this is
    /// the fallback for a quest id the catalogue no longer knows.
    /// </summary>
    public string? QuestText { get; init; }
}

/// <summary>
/// One playable character: a fixed appearance, an aptitude, and one passive nobody else has.
/// </summary>
/// <remarks>
/// <para>
/// A character is NOT a second progression tree. The mastery and trait trees are shared across the
/// whole roster and nothing about them resets when you switch — that is the game's oldest promise, and
/// a per-character tree would have broken it while tripling the save format. What a character changes
/// is the shape of the build you already have.
/// </para>
/// <para>
/// Three levers, and every one of them is read by the sim rather than printed on a card:
/// </para>
/// <list type="bullet">
/// <item><b>Lean</b> — which of the four opposed roads this character was built to walk. Advisory: it
/// colours the roster screen and tells a new player where to spend, and costs nothing.</item>
/// <item><b>Aptitude</b> — a real per-Form damage multiplier through <see cref="SkillShape.FormPower"/>.</item>
/// <item><b>Passive</b> — <see cref="Mods"/>, <see cref="Shape"/> and <see cref="Grants"/>, folded into
/// the build at <c>PlayerLoadout.ToBuild</c> alongside the two trees.</item>
/// </list>
/// </remarks>
public sealed record Character
{
    public required string Id { get; init; }

    /// <summary>The name on the card — "THE ANVIL".</summary>
    public required string Name { get; init; }

    /// <summary>One line of who they are.</summary>
    public required string Blurb { get; init; }

    /// <summary>The road they were built for, or null for a character who favours none.</summary>
    public Branch? Lean { get; init; }

    /// <summary>
    /// The ITEM CLASS this champion wears — see <see cref="Economy.ItemClasses"/>. Weapon, helm, chest,
    /// gloves and boots of another class cannot be worn; charm, ring and focus can always be.
    /// </summary>
    /// <remarks>
    /// Required, not defaulted: a champion whose class was silently WANDERER would wear every weapon
    /// shape and read as the starter, which is the quiet kind of wrong this codebase is built to avoid.
    /// </remarks>
    public required Economy.ItemClass Class { get; init; }

    /// <summary>
    /// FIRST or SECOND of the class — see <see cref="ClassTier"/>. Required, so a champion added to
    /// the roster has to say which of its class's two it is, and the tests can hold it to the gate that
    /// tier implies.
    /// </summary>
    public required ClassTier Tier { get; init; }

    /// <summary>The Form they are better at than you are.</summary>
    public Form? Aptitude { get; init; }

    /// <summary>
    /// The one skill this champion already knows, before a single mastery point is spent.
    /// </summary>
    /// <remarks>
    /// Every one of the twelve is learned on the mastery tree now, so without this a fresh champion
    /// would stand in its first wave with nothing to weave. The designer chose it as the answer AND
    /// as a character rule: "Her karakterin basic pasif veya aktif bir skilli olabilir. Böylelikle
    /// karakterlerin de ayrımı daha net olabilir." Some bring an active and some a passive, which is
    /// a sharper difference between two champions than an aptitude multiplier is.
    /// </remarks>
    public string? StartingSkillId { get; init; }

    /// <summary>How much better. 1.25 = +25% on that Form's skills.</summary>
    public float AptitudePower { get; init; } = 1.25f;

    public required string PassiveName { get; init; }
    public required string PassiveText { get; init; }

    /// <summary>The passive's flat multipliers.</summary>
    public BuildMods Mods { get; init; } = BuildMods.None;

    /// <summary>The passive's shape changes — the same bag the mastery tree fills.</summary>
    public SkillShape Shape { get; init; } = SkillShape.None;

    /// <summary>Behaviours the passive turns on.</summary>
    public IReadOnlyList<BuildTrigger> Grants { get; init; } = Array.Empty<BuildTrigger>();

    public CharacterUnlock Unlock { get; init; } = CharacterUnlock.Start;

    /// <summary>The name without its article — "SEEKER" for "THE SEEKER". For the HUD, where width is scarce.</summary>
    public string ShortName =>
        Name.StartsWith("THE ", System.StringComparison.OrdinalIgnoreCase) ? Name[4..] : Name;

    /// <summary>"FIRST OF THE WARDENS" — the line under the name that says which of the class's two this is.</summary>
    public string TierLine =>
        $"{(Tier == ClassTier.First ? "FIRST" : "SECOND")} OF THE {Economy.ItemClasses.PluralOf(Class)}";

    /// <summary>Base sprite key — <c>char_&lt;id&gt;_base</c>.</summary>
    public string SpriteKey => $"char_{Id}_base";

    /// <summary>Animation strip key for a clip — <c>char_&lt;id&gt;_&lt;clip&gt;_strip8_512</c>.</summary>
    public string StripKey(string clip) => $"char_{Id}_{clip}_strip8_512";

    /// <summary>
    /// The strip keys to try for a clip, most specific first — a character's OWN clip for a Form, then
    /// the generic one it falls back to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every Form used to share two clips: <c>attack</c> for a Strike and <c>cast</c> for everything
    /// else. Ten characters therefore threw the same two shapes for five very different verbs — the
    /// Quiver's Projectile and the Anvil's Transformation were one raised hand apiece. The contract's
    /// clip table (design/art/arena-art-contract.md §3) now carries a clip per Form per character.
    /// </para>
    /// <para>
    /// THE FALLBACK IS THE POINT. Fifty strips do not arrive at once, and a character whose Projectile
    /// clip has not been generated yet must keep casting rather than freeze: an asset that is missing
    /// resolves to the generic clip, and a clip that is missing resolves to the idle at the draw site.
    /// So the code can ship ahead of the art without a single blank frame.
    /// </para>
    /// </remarks>
    public IEnumerable<string> StripKeys(string clip)
    {
        yield return StripKey(clip);
        var generic = GenericClipFor(clip);
        if (generic is not null) yield return StripKey(generic);
    }

    /// <summary>Which of the two original clips a per-Form clip stands in for, or null if it IS one.</summary>
    public static string? GenericClipFor(string clip) => clip switch
    {
        // A Strike is the heavy swing; the rest are casts. Trap has no generic — it is a new clip and a
        // character without one simply does not act when its trap bites, which is what happens today.
        "strike" => "attack",
        "projectile" or "mark" or "transformation" or "aura" => "cast",
        "trap" => null,
        _ => null,
    };

    /// <summary>
    /// HUD portrait key — <c>char_&lt;id&gt;_portrait</c>. The screens that used to draw the one generic
    /// <c>hunter_portrait</c> (HUNT, STATS, CHARACTER, BUILD) ask for this first and fall back to it.
    /// </summary>
    public string PortraitKey => $"char_{Id}_portrait";

    /// <summary>The aptitude expressed as the shape the sim actually reads.</summary>
    public SkillShape AptitudeShape =>
        Aptitude is { } f
            ? new SkillShape { FormPower = new Dictionary<Form, float> { [f] = AptitudePower } }
            : SkillShape.None;

    /// <summary>Everything this character contributes to a build, as one shape.</summary>
    public SkillShape TotalShape => SkillShape.Combine(Shape, AptitudeShape);
}
