using System;
using System.Globalization;

namespace IdleXIdle.Game.Presentation;

/// <summary>
/// THE CAPTURE RIG'S REVIEW VIEWS, in one place: presentation-QA switches the capture tooling sets through the
/// environment so a film can show one layer of the fight's language at a time. The game never sets them; every one is
/// inert without its variable, read once, and consulted by the arena's draw and layout only.
/// </summary>
/// <remarks>
/// Each is GENERIC (a layer on or off, a dial overridden), never "show study variant X": a one-off study knob is a
/// prototype's, and is removed when the study is filed (the 2026-09-26 foundation study's `RH_SHOT_NOANSWER`,
/// `RH_SHOT_FLASH=F1|F2|F3`, `RH_SHOT_BITE`, `RH_SHOT_HITSTOP` were). Documented for the tooling in
/// <c>tools/asset-pipeline/README-capture-views.md</c>.
/// <list type="bullet">
/// <item><c>RH_SHOT_NOVFX=1</c> — no effects, no performed material or light, no reaction layer (does the animation read alone?)</item>
/// <item><c>RH_SHOT_NOCHAMP=1</c> — the champion is not drawn (does the force path read alone?)</item>
/// <item><c>RH_SHOT_SOCKETS=1</c> — the hand sockets drawn as crosshairs</item>
/// <item><c>RH_SHOT_NOTINT=1</c> — no ember wind-up tint on the creatures</item>
/// <item><c>RH_SHOT_SIL=1</c> — every creature drawn as a flat silhouette (does the SHAPE perform the action?)</item>
/// <item><c>RH_SHOT_NOROOT=1</c> — the pack's presentation lunge off (the creatures stay in their row)</item>
/// <item><c>RH_SHOT_LUNGE=&lt;share&gt;</c> — the leader's lunge travel as a share of its visible width (a tuning dial)</item>
/// <item><c>RH_SHOT_NORECOIL=1</c> — the champion's hit recoil off (the bite motif is not the recoil's and stays; <c>RH_SHOT_NOVFX</c> hides it)</item>
/// <item><c>RH_SHOT_FLASH=off | peak,rise,ms</c> — the generic hit flash off, or overridden for every hit (a recipe's included), e.g. <c>1,0.2,200</c> is the old full-white flash</item>
/// <item><c>RH_SHOT_RECOIL=&lt;share&gt;</c> — the champion's hit recoil reach as a share of his visible width (a tuning series from one build)</item>
/// <item><c>RH_SHOT_STRIP_FILES=key=path;...</c> — textures loaded from outside the asset tree under a key (<c>AssetLibrary</c>): a stand-in body, a prototype strip</item>
/// </list>
/// </remarks>
public static class CaptureViews
{
    public static readonly bool NoVfx = Flag("RH_SHOT_NOVFX");
    public static readonly bool NoChampion = Flag("RH_SHOT_NOCHAMP");
    public static readonly bool Sockets = Flag("RH_SHOT_SOCKETS");
    public static readonly bool NoTint = Flag("RH_SHOT_NOTINT");
    public static readonly bool Silhouette = Flag("RH_SHOT_SIL");
    public static readonly bool NoRootMotion = Flag("RH_SHOT_NOROOT");
    public static readonly bool NoRecoil = Flag("RH_SHOT_NORECOIL");

    /// <summary>The flash override: null when not set, (0,0,0) for <c>off</c>, else the given (peak, rise, rate per second).</summary>
    public static readonly (float Peak, float Rise, float Rate)? FlashOverride = ParseFlash(Environment.GetEnvironmentVariable("RH_SHOT_FLASH"));

    /// <summary>The champion's recoil reach override (<c>RH_SHOT_RECOIL=&lt;share of visible width&gt;</c>): a dial the rig
    /// turns to film a tuning series from one build; null when not set.</summary>
    public static readonly float? RecoilOverride =
        float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_RECOIL"), NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : null;

    /// <summary>The leader's lunge travel override (<c>RH_SHOT_LUNGE=&lt;share of visible width&gt;</c>): a dial the rig
    /// turns to film 20 / 25 / 30 % from one build; null when not set.</summary>
    public static readonly float? LungeOverride =
        float.TryParse(Environment.GetEnvironmentVariable("RH_SHOT_LUNGE"), NumberStyles.Float, CultureInfo.InvariantCulture, out var l) ? l : null;

    private static bool Flag(string name) => Environment.GetEnvironmentVariable(name) == "1";

    private static (float, float, float)? ParseFlash(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return null;
        spec = spec.Trim();
        if (spec.Equals("off", StringComparison.OrdinalIgnoreCase)) return (0f, 0f, 0f);
        var parts = spec.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3
            || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var peak)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var rise)
            || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) || ms <= 0f)
            throw new InvalidOperationException($"RH_SHOT_FLASH='{spec}' is not 'off' or 'peak,rise,ms'.");
        return (peak, rise, 1000f / ms);
    }
}
