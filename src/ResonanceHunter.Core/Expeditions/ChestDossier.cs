using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Encounters;
using ResonanceHunter.Core.Loot;

namespace ResonanceHunter.Core.Expeditions;

/// <summary>
/// Everything that is truthfully knowable about a chest BEFORE it is opened.
/// </summary>
/// <remarks>
/// <para>
/// Playtest: <i>"Ne chesti düştüğünü bilmiyorum, açınca anlıyorum sadece."</i> — I do not know what
/// chest dropped, I only find out when I open it. That was accurate. The Forge showed a single line,
/// <c>"3 CHESTS"</c>, tinted by the best grade in the pile, and nothing else. A chest already carries
/// a grade, a tier, an element, a region and the tilt of the run that won it, and every one of those
/// decides something concrete about the contents — all of it invisible.
/// </para>
/// <para>
/// <b>Every field here is DERIVED from <see cref="ChestTuning"/> and the chest itself, never written
/// out by hand.</b> That is the whole discipline of this file: a screen that describes a reward is a
/// second statement of the rules, and a second statement drifts. When the tuning is retuned, this
/// text changes with it or it does not change at all — it cannot silently start lying.
/// </para>
/// <para>
/// It also deliberately describes only what is DECIDED at drop. The contents are rolled at open (see
/// <see cref="Chests.Open"/>), so this promises a floor and a range, never an outcome. Telling the
/// player exactly what is inside would delete the reason the chest exists.
/// </para>
/// </remarks>
public sealed record ChestDossier
{
    /// <summary>The grade rolled when the boss dropped it — the headline.</summary>
    public required Rarity Grade { get; init; }

    /// <summary>The loot tier its items roll at.</summary>
    public required int Tier { get; init; }

    /// <summary>The region's element; items inside are attuned to it. Null for an unattuned chest.</summary>
    public Source? Element { get; init; }

    /// <summary>Where it was won, or null for a chest from before regions had drop profiles.</summary>
    public string? Region { get; init; }

    /// <summary>
    /// The rarity the chest GUARANTEES it will not go below.
    /// </summary>
    /// <remarks>
    /// The single most valuable thing to show, and the one the player had no way to learn. It is what
    /// makes a good chest reliably good — <see cref="Chests.Open"/> elevates every item up to this
    /// floor — and it is the difference between "a Legendary chest is exciting" and "a Legendary chest
    /// is a lottery ticket that can still pay nothing".
    /// </remarks>
    public required Rarity GuaranteedFloor { get; init; }

    public required int MinItems { get; init; }
    public required int MaxItems { get; init; }
    public required int MinMaterials { get; init; }
    public required int MaxMaterials { get; init; }

    /// <summary>The slots this chest's region favours. Empty means no lean.</summary>
    public required IReadOnlyList<ItemBaseType> Favoured { get; init; }

    /// <summary>How well the descent that won it was fought. 1 is neutral; above 1 is better odds.</summary>
    public required float RunTilt { get; init; }

    /// <summary>What the place is, in the map's own words. Empty when the chest predates region profiles.</summary>
    public required string RegionBlurb { get; init; }

    /// <summary>The gift this chest is, or null for a chest that rolls. See <see cref="GiftChests"/>.</summary>
    public GiftChestDef? Gift { get; init; }

    /// <summary>Is this a gift — contents decided, nothing to gamble on?</summary>
    public bool IsGift => Gift is not null;

    // ── The player-facing lines. Each states one fact and nothing else. ──────────────────────────

    /// <summary>The headline over the dossier — "RARE CHEST", or a gift's own title.</summary>
    public string Title => Gift?.Title ?? $"{Grade.ToString().ToUpperInvariant()} CHEST";

    /// <summary>The promise: what this chest cannot fail to give.</summary>
    /// <remarks>
    /// A Common and an Uncommon chest share a Common floor, so for those the honest line is that there
    /// is no floor worth naming. Claiming "guaranteed Common" would be technically true and read as a
    /// guarantee, which is worse than saying nothing.
    /// </remarks>
    public string FloorLine => GuaranteedFloor <= Rarity.Common
        ? "Any rarity can come out — a gamble."
        : $"Always {GuaranteedFloor.ToString().ToUpperInvariant()} or better.";

    /// <summary>What comes out, in count and level.</summary>
    public string ContentsLine => MinItems == MaxItems
        ? $"{MinItems} item at tier {Tier}."
        : $"{MinItems}–{MaxItems} items at tier {Tier}.";

    /// <summary>The material payout.</summary>
    public string MaterialsLine => $"{MinMaterials}–{MaxMaterials} materials.";

    /// <summary>The attunement, or that there is none.</summary>
    public string ElementLine => Element is { } e
        ? $"Element: {e.ToString().ToUpperInvariant()}."
        : "Plain — no element.";

    /// <summary>The player-facing name of a gear slot. <c>AbilityFocus</c> is called a FOCUS everywhere.</summary>
    /// <remarks>
    /// The enum name leaked into the UI as "ABILITYFOCUS", which appears nowhere else in the game — the
    /// Forge and the Gear screen both call it a FOCUS. One vocabulary, or the player has to work out
    /// that two words are the same slot.
    /// </remarks>
    private static string SlotName(ItemBaseType t) => t switch
    {
        ItemBaseType.AbilityFocus => "FOCUS",
        ItemBaseType.CreatureCore => "CORE",
        _ => t.ToString().ToUpperInvariant(),
    };

    /// <summary>What the place it came from leans toward.</summary>
    public string RegionLine
    {
        get
        {
            if (Favoured.Count == 0) return "No favoured gear.";
            return $"Favours {string.Join(" and ", Favoured.Select(SlotName))}.";
        }
    }

    /// <summary>The same fact at card width — no verb, no punctuation, just the slots.</summary>
    /// <remarks>
    /// A card is 256px wide and the sentence form truncated to "Favours HELM and CHEST an…", which
    /// costs the reader the one word that mattered. Two vocabularies for one fact would be worse, so
    /// both forms are derived from the same list.
    /// </remarks>
    public string RegionShort => Gift is { } g ? g.CardContents
        : Favoured.Count == 0 ? "no lean" : string.Join(" · ", Favoured.Select(SlotName));

    /// <summary>The floor promise at card width — or, on a gift, the word that it is one.</summary>
    public string FloorShort => Gift is { } g ? g.CardPromise
        : GuaranteedFloor <= Rarity.Common
            ? "any rarity"
            : $"{GuaranteedFloor.ToString().ToUpperInvariant()} or better";

    /// <summary>The run's own tilt, mentioned only when it actually did something.</summary>
    /// <remarks>
    /// Silent at neutral. A line that appears on every chest saying "+0%" is a line the player learns
    /// to skip, and it would bury the case where the tilt is real.
    /// </remarks>
    public string? RunLine => MathF.Abs(RunTilt - 1f) < 0.02f
        ? null
        : RunTilt > 1f
            ? $"Won on a good hunt — {(RunTilt - 1f) * 100f:0}% better odds."
            : $"Won on a bad hunt — {(1f - RunTilt) * 100f:0}% worse odds.";

    /// <summary>Every line worth drawing, in reading order, with the empty ones dropped.</summary>
    /// <remarks>A gift's lines are its own: it has no floor, no range and no lean — it has contents.</remarks>
    public IReadOnlyList<string> Lines
    {
        get
        {
            if (Gift is { } g) return g.Lines;
            var lines = new List<string> { FloorLine, ContentsLine, MaterialsLine, ElementLine, RegionLine };
            if (RunLine is { } r) lines.Add(r);
            return lines;
        }
    }
}

/// <summary>Reads a chest and reports what can honestly be said about it unopened.</summary>
public static class ChestDossiers
{
    /// <summary>Describe one chest.</summary>
    public static ChestDossier For(Chest chest, ChestTuning? tuning = null)
    {
        ArgumentNullException.ThrowIfNull(chest);
        tuning ??= ChestTuning.Default;

        var profile = RegionDrops.For(chest.Region);
        var gift = GiftChests.Get(chest.Gift);

        // Mirrors Chests.Open exactly. Every term is read from the same tuning the roll reads, so the
        // two cannot disagree — the alternative (writing "12-18 materials" into a string) is a copy of
        // the rules that goes stale the first time anyone touches BaseMaterials.
        var tierMaterials = chest.Tier / Math.Max(1, tuning.TiersPerBonusMaterial);

        return new ChestDossier
        {
            Grade = chest.Rarity,
            Tier = chest.Tier,
            Element = chest.Element,
            Region = chest.Region,
            RunTilt = chest.RunTilt,
            GuaranteedFloor = tuning.ItemFloorByGrade[(int)chest.Rarity],
            MinItems = tuning.MinItems,
            MaxItems = tuning.MinItems + (tuning.ExtraItemChance > 0f ? 1 : 0),
            MinMaterials = tuning.BaseMaterials + tierMaterials,
            MaxMaterials = tuning.BaseMaterials + tuning.MaterialsVariance + tierMaterials,
            Favoured = profile.Favoured,
            // A gift was not WON anywhere, so it carries no "where it was won" — the region only
            // lends it an element. A rolled chest keeps its place.
            RegionBlurb = gift is null ? profile.Blurb : "",
            Gift = gift,
        };
    }

    /// <summary>
    /// Sort a pile the way a player wants to see it: best first, then deepest.
    /// </summary>
    /// <remarks>
    /// The Forge's existing OPEN button already opens the BEST chest, so a list in any other order
    /// would put the button's target somewhere other than the top of what the player is reading.
    /// </remarks>
    public static IReadOnlyList<Chest> BestFirst(IEnumerable<Chest> chests)
    {
        ArgumentNullException.ThrowIfNull(chests);
        return chests.OrderByDescending(c => (int)c.Rarity)
                     .ThenByDescending(c => c.Tier)
                     .ToList();
    }

    /// <summary>
    /// The pile grouped into STACKS of identical chests, best first — the vault draws one card per stack
    /// with a ×N. Identity is what the dossier prints: grade, tier, element, the region's lean, and the
    /// run tilt at the two-decimal resolution <see cref="ChestDossier.RunLine"/> already treats as the
    /// truth boundary, so two chests that stack are two chests whose card would read the same.
    /// </summary>
    /// <remarks>
    /// Display-only grouping: contents are rolled at OPEN (<see cref="Chests.Open"/>), so a stack needs
    /// no per-chest state and opening one from it is just opening any member. The sample is a real
    /// member, so the host can find it in storage by record equality.
    /// </remarks>
    public static IReadOnlyList<(Chest Sample, int Count)> Stacked(IEnumerable<Chest> chests)
    {
        ArgumentNullException.ThrowIfNull(chests);
        // The gift key is part of the identity: a welcome gift must never stack under a Common tier-1
        // chest from the same region, because the two cards would not read the same.
        return BestFirst(chests)
            .GroupBy(c => (c.Rarity, c.Tier, c.Element, c.Region, Tilt: MathF.Round(c.RunTilt, 2), c.Gift))
            .Select(g => (g.First(), g.Count()))
            .ToList();
    }

    /// <summary>How many of each grade are waiting — the one-line summary above the list.</summary>
    public static IReadOnlyList<(Rarity Grade, int Count)> Tally(IEnumerable<Chest> chests)
    {
        ArgumentNullException.ThrowIfNull(chests);
        return chests.GroupBy(c => c.Rarity)
                     .OrderByDescending(g => (int)g.Key)
                     .Select(g => (g.Key, g.Count()))
                     .ToList();
    }
}
