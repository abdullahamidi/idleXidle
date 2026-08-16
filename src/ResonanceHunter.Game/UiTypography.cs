namespace ResonanceHunter.Client;

/// <summary>
/// Central typography tokens (Hunt spec rev 4 §23). Screen code should size text from these named roles
/// rather than inline magic numbers, so the type hierarchy stays consistent and above the minimum sizes.
/// Values are LOGICAL pixel heights in the 1920×1080 authoring space.
/// </summary>
public static class UiTypography
{
    public const int RegionTitle = 36;
    public const int ScreenTitle = 36;
    public const int StageLabel = 26;
    public const int SectionTitle = 26;
    public const int PanelTitle = 24;
    public const int PrimaryValue = 30;
    public const int Body = 19;
    public const int Secondary = 16;

    /// <summary>
    /// The size an UNSIZED <c>_ui.Text</c> draws at. The default for a label.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It used to be 32 — larger than every token here except ScreenTitle.</b> That is not a
    /// stylistic quibble; it inverted the hierarchy of half the game. 146 call sites use the unsized
    /// proxies, and they are almost all labels and captions, so the biggest text on a screen was
    /// routinely its least important line: on GEAR an affix row outsized the item's own name, on ROSTER
    /// the word "READY" was twice the size of the champion it belonged to, on HELP every body row
    /// outsized the panel title above it.
    /// </para>
    /// <para>
    /// Changed in ONE place rather than at 146 call sites, deliberately. A sweep of that size cannot be
    /// verified by reading it — this can be verified by looking at every screen, which is what was done.
    /// A site that genuinely wants to shout can still say so with the sized <c>*Big</c> proxies.
    /// </para>
    /// </remarks>
    public const int Label = 18;
    public const int NavigationLabel = 21;
    public const int OverlayTitle = 22;
    public const int OverlayBody = 18;
    public const int DamageNormal = 30;
    public const int DamageCritical = 40;
}
