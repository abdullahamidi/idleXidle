namespace ResonanceHunter.Core.Economy;

/// <summary>
/// A single-use paper that pays for one Forge operation.
/// </summary>
/// <remarks>
/// <para>
/// Playtest, two passes ago: <i>"Waveler de materyal-yükseltme-reforge-salvage-merge kağıdı gibi
/// yardımcı itemler atsın"</i> — waves should also drop helper items, like materials and
/// upgrade/reforge/salvage/merge papers.
/// </para>
/// <para>
/// <b>Each one waives a cost or lifts a rule — never both, and never a new number to grind.</b> They
/// are deliberately not a fifth material tier: the Forge already has four, and a currency you
/// accumulate toward a threshold is exactly the kind of thing an ordinary wave should NOT be paying
/// out. A charter is a single permission, spent whole, on the operation it names.
/// </para>
/// <para>
/// This matters more after the Gleam retune than it would have before it. Refining costs Gleam, and
/// Gleam is now genuinely scarce, so a REFINE CHART is a real gift rather than a rounding error — and
/// a REFORGE CHART is the pity currency for the design rule that rolled properties should make the item
/// you actually want hard to get.
/// </para>
/// </remarks>
public enum Charter
{
    /// <summary>One refine, free — no Scrap, no Gleam.</summary>
    Refine,

    /// <summary>One reforge, free — the trait or the enchantment, whichever you re-roll.</summary>
    Reforge,

    /// <summary>One salvage, at double the materials.</summary>
    Salvage,

    /// <summary>One merge that ignores the same-rarity rule.</summary>
    Merge,
}

/// <summary>What each charter is called and what it actually does.</summary>
/// <remarks>
/// Text lives beside the enum rather than in a screen, for the reason everything else in this codebase
/// does: a screen that describes a rule is a second statement of it, and the second one drifts. A test
/// asserts every charter has both.
/// </remarks>
public static class Charters
{
    public static string Name(Charter c) => c switch
    {
        Charter.Refine => "REFINE CHART",
        Charter.Reforge => "REFORGE CHART",
        Charter.Salvage => "SALVAGE CHART",
        _ => "MERGE CHART",
    };

    /// <summary>One line, stating the exact permission — never a vague "helps with X".</summary>
    public static string Blurb(Charter c) => c switch
    {
        Charter.Refine => "One REFINE, free — no Scrap and no Gleam.",
        Charter.Reforge => "One REFORGE, free — trait or enchantment, whichever you re-roll.",
        Charter.Salvage => "One SALVAGE, at double materials.",
        _ => "One MERGE that ignores the same-rarity rule — the lowest of the three sets the grade.",
    };

    /// <summary>Short label for a currency pill or a toolbar chip.</summary>
    public static string Short(Charter c) => c switch
    {
        Charter.Refine => "REFINE",
        Charter.Reforge => "REFORGE",
        Charter.Salvage => "SALVAGE",
        _ => "MERGE",
    };
}
