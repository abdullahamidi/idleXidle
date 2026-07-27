namespace ResonanceHunter.Client;

/// <summary>
/// Central typography tokens (Hunt spec rev 4 §23). Screen code should size text from these named roles
/// rather than inline magic numbers, so the type hierarchy stays consistent and above the minimum sizes.
/// Values are LOGICAL pixel heights in the 1920×1080 authoring space.
/// </summary>
public static class UiTypography
{
    public const int RegionTitle = 36;
    public const int StageLabel = 26;
    public const int PanelTitle = 24;
    public const int PrimaryValue = 30;
    public const int Body = 19;
    public const int Secondary = 16;
    public const int NavigationLabel = 21;
    public const int OverlayTitle = 22;
    public const int OverlayBody = 18;
    public const int DamageNormal = 30;
    public const int DamageCritical = 40;
}
