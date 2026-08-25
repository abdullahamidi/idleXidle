using System;
using System.Collections.Generic;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Builds;

namespace ResonanceHunter.Core.Characters;

/// <summary>How a character is earned.</summary>
/// <remarks>
/// Quests do not exist yet. The kind is declared anyway because the alternative — bolting a second
/// unlock mechanism on later — is how a system ends up with two of everything, and this codebase has
/// been bitten by parallel systems more than once. A quest-gated character reads as LOCKED and names
/// its quest; the day quests land, <see cref="CharacterState.QuestDone"/> starts returning true and
/// nothing else has to change.
/// </remarks>
public enum UnlockKind
{
    /// <summary>Yours from the first session.</summary>
    Start,

    /// <summary>Earned by conquering a region — see <see cref="CharacterUnlock.RegionId"/>.</summary>
    Conquest,

    /// <summary>Earned by finishing a quest. The hook; nothing drives it yet.</summary>
    Quest,
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

    /// <summary>Player-facing text for a quest gate, since there is no quest catalogue to read yet.</summary>
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

    /// <summary>The Form they are better at than you are.</summary>
    public Form? Aptitude { get; init; }

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

    /// <summary>Base sprite key — <c>char_&lt;id&gt;_base</c>.</summary>
    public string SpriteKey => $"char_{Id}_base";

    /// <summary>Animation strip key for a clip — <c>char_&lt;id&gt;_&lt;clip&gt;_strip8_512</c>.</summary>
    public string StripKey(string clip) => $"char_{Id}_{clip}_strip8_512";

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
