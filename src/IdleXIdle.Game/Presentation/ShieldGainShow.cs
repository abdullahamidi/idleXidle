namespace IdleXIdle.Game.Presentation;

/// <summary>What the hunt shows for one ShieldGained (design.md 4 SHIELD and 5.29).</summary>
/// <param name="Rim">The shield bar's rim flare: ALWAYS, the bar is where the gain landed.</param>
/// <param name="Cue">The generic <c>sfx_shield_gain</c>.</param>
/// <param name="OneShot">The generic <c>ShieldGain</c> flare on the barrier.</param>
/// <param name="Number">The "+N SHIELD" callout (still gated by DAMAGE NUMBERS by the caller).</param>
public readonly record struct ShieldGainShow(bool Rim, bool Cue, bool OneShot, bool Number)
{
    /// <summary>
    /// The decision: a recipe's CLAIMED gain keeps only the rim (its own picture and cue replace the rest); a WAVE-OPEN
    /// gain (ms 0: GROUNDWORK, CARRIED, the rungs' opening grant) is shown silently, the barrier simply up; any other gain
    /// is the existing grammar unchanged.
    /// </summary>
    public static ShieldGainShow For(int atMs, bool claimed)
    {
        var generic = !claimed && atMs > 0;
        return new ShieldGainShow(Rim: true, Cue: generic, OneShot: generic, Number: generic);
    }
}
