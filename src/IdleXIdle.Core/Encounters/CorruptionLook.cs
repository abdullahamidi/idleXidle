namespace IdleXIdle.Core.Encounters;

/// <summary>
/// How a corruption tier LOOKS and is NAMED — the presentation's half of the ladder, kept in Core so
/// the map, the hunt and the tests read one table.
/// </summary>
/// <remarks>
/// The fight tints every creature and boss by <see cref="Enemy"/> and the arena by <see cref="Arena"/>
/// (a multiply tint, so white is "as drawn"), and the boss wears the tier's <see cref="Epithet"/>
/// ("FEVERED CRYSTAL LICH"). Colours walk from nothing toward Umbra Indigo: a corrupted world is a
/// place with the light taken out of it (art bible §4.2), not a brighter one. Plain-English names, per
/// the house copy rule — the tier number is shown beside them, never instead of them.
/// </remarks>
public static class CorruptionLook
{
    public readonly record struct Look(string Name, string Epithet, string Blurb,
                                       (byte R, byte G, byte B) Enemy, (byte R, byte G, byte B) Arena);

    private static readonly Look[] Table =
    {
        new("THE WORLD AS IT IS", "",          "The normal world. Nothing extra.",                      (255, 255, 255), (216, 216, 224)),
        new("STIRRING",           "STIRRING",  "Enemies are tougher. A little more Memory Dust.",       (242, 232, 248), (206, 202, 222)),
        new("RESTLESS",           "RESTLESS",  "Less light. Enemies hit harder and take more hits.",    (230, 212, 244), (196, 188, 220)),
        new("FEVERED",            "FEVERED",   "The world runs hot. Bosses are now named FEVERED.",     (220, 192, 238), (184, 172, 214)),
        new("RAVENOUS",           "RAVENOUS",  "Deep corruption. Even more Memory Dust here.",           (214, 172, 232), (170, 154, 206)),
        new("ABYSSAL",            "ABYSSAL",   "The bottom of the world. Nothing is deeper. Nothing pays more.",               (208, 150, 226), (156, 136, 198)),
    };

    /// <summary>The look of a tier (clamped into the ladder).</summary>
    public static Look For(int corruptionTier) => Table[CorruptionScaling.Clamp(corruptionTier)];

    /// <summary>"CORRUPTION 3 / 5 · FEVERED" — the one line every screen uses for the tier.</summary>
    public static string Label(int corruptionTier)
    {
        var t = CorruptionScaling.Clamp(corruptionTier);
        return t == 0 ? "CORRUPTION 0 / " + CorruptionScaling.MaxTier + " · " + For(0).Name
                      : $"CORRUPTION {t} / {CorruptionScaling.MaxTier} · {For(t).Name}";
    }
}
