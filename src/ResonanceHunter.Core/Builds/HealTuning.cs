using System;

namespace ResonanceHunter.Core.Builds;

/// <summary>
/// Every knob for SKILL and BUILD healing — what a Transformation leeches, what the Nature signature
/// returns, what SIPHON does, and the ceiling on how much a build may regain in one wave.
/// </summary>
/// <remarks>
/// <para>
/// Playtest 2026-08-25: <i>"The health-recovery skills are far too strong (broken)."</i> Measured by
/// <c>heal_balance_test</c> on the balance sweep's geared mid-career fixture, sixteen seeds each,
/// against the standard curve (the numbers below are that test's LEGACY rows):
/// </para>
/// <code>
///   build                     depth   healed/wave  bitten/wave  heal:dmg  survived bites > pool
///   MORPH x4                     62       117%         118%       0.99        18% of waves
///   MORPH x4, no heals           28        0%            4%       0.03         0%
///   MORPH x4 + SIPHON            67       288%         289%       1.00        25% of waves
///   fresh champion, MORPH x4     38       260%         263%       0.99        47% of waves
///   fresh champion, no heals      4
/// </code>
/// <para>
/// And the same rows under the live numbers: MORPH x4 47 (1.68x its twin, heal:dmg 0.83, 0% of
/// waves survived past the pool, died in 16 of 16 runs); MORPH x4 + SIPHON 49 (1.75x); four Nature
/// Auras 32 against 19 (1.68x); the fresh champion 10 against 4.
/// </para>
/// <para>
/// Read the middle columns, not the depth: a four-Transformation build was being bitten for MORE THAN
/// ITS WHOLE POOL every wave and healing all of it back. Its pool was not keeping it alive; its leech
/// was, and the pool had become a formality. A fresh champion — the pacing fixture, which the first
/// region is tuned to kill between waves 4 and 20 — walked to wave 38 on four Transformations.
/// </para>
/// <para>
/// WHY A CEILING AND NOT JUST A SMALLER NUMBER. A leech is a fraction of damage DEALT, and damage
/// dealt per wave is the wave's health, which compounds 6% a wave while the champion's pool does not.
/// So any leech fraction is exponential in depth and only a ceiling relative to the pool bounds it.
/// Swept (leech x ceiling, geared MORPH x4 median depth / MORPH+SIPHON / fresh MORPH x4):
/// </para>
/// <code>
///   leech   ceiling 25%   30%   40%   50%   none
///   0.06         45/45   46/46 47/47 49/49  50/51 (fresh 10)
///   0.08         45/45   46/46 47/47 49/49  51/53 (fresh 10)
///   0.12         45/45   46/46 47/47 49/49  51/60 (fresh 10-11)
///   0.15         45/45   46/46 47/47 49/49  52/60 (fresh 10-13)
/// </code>
/// <para>
/// Under a ceiling the depth is the ceiling's, whatever the leech says; the leech fraction decides how
/// EARLY a build reaches it, and how much a low-damage build (a fresh champion, one Transformation in
/// four slots) gets back at all. 0.12 keeps a fresh Transformation build healing about 70% of what it
/// takes and dying at wave 10 against its twin's 4 — inside the first region's band — and 40% is the
/// asked-for ceiling, one wave either side of its neighbours.
/// </para>
/// <para>
/// SIPHON. The sweep's last column is the whole story of that enchant: its doubling only ever bought
/// depth with NO ceiling (51 → 60), i.e. in the regime the playtest called broken, and under any
/// ceiling it was dormant (47 → 47). A card that says "heals 2x" and measures 0 is the house bug. So
/// SIPHON now also raises its wearer's ceiling by <see cref="SiphonCeilingMultiplier"/> (40% → 60%),
/// which measures +2 waves on the geared fixture with the over-pool share still ~1% — small, real,
/// and bounded, which is what an enchant should be.
/// </para>
/// <para>
/// What the ceiling COVERS, and why. Every heal that lands inside <c>SoloBattle.ResolveWave</c> counts
/// — the LEECH node, FEEDBACK's heal per target, the Transformation's own leech, SIPHON, the Nature
/// signature, and SECOND WIND's on-clear heal (it fires inside the wave's own resolution, as a reward
/// for the clear, and a reward that could exceed the ceiling would be the loophole). RECOVERY's
/// between-wave regain is deliberately NOT counted: it lives in <see cref="SoloExpedition"/> outside
/// the wave, it is bounded by construction (a flat fraction of the pool, once, never more than its
/// own number), the ENDLESS band already halves it, and design/gdd/game-flow.md §3.3 prices it as the
/// one structural exception to "health does not regenerate between waves". Counting it against the
/// in-wave budget would delete the Notable on exactly the builds that walk to it. ENDLESS (the mastery,
/// full health every wave) is a reset, not regeneration, and stays untouched for the same reason.
/// </para>
/// <para>
/// Not VITALITY. The trained stat's passive regeneration is a separate channel with its own owner and
/// is not read here.
/// </para>
/// </remarks>
public sealed record HealTuning
{
    /// <summary>TRANSFORMATION returns this fraction of the damage it deals as health.</summary>
    /// <remarks>Was 0.50. See the type remarks for the sweep that chose 0.12.</remarks>
    public float TransformationLeech { get; init; } = 0.12f;

    /// <summary>SIPHON (the Transformation combo enchantment) multiplies the Form's leech by this.</summary>
    public float SiphonMultiplier { get; init; } = 2.0f;

    /// <summary>SIPHON also multiplies its wearer's <see cref="MaxHealFractionPerWave"/> by this.</summary>
    /// <remarks>
    /// The half of SIPHON that is measurable under a ceiling. 1.5 takes the default ceiling from 40%
    /// to 60% of the pool per wave; measured +2 waves on the geared fixture, over-pool share ~1%.
    /// </remarks>
    public float SiphonCeilingMultiplier { get; init; } = 1.5f;

    /// <summary>NATURE's signature — its skills heal for this sliver of what they deal.</summary>
    /// <remarks>Kept small on purpose: a signature is glue, not payload (see SoloBattle's signature notes).</remarks>
    public float NatureSignatureLeech { get; init; } = 0.03f;

    /// <summary>
    /// The most a build may regain from skills inside ONE wave, as a fraction of maximum health.
    /// </summary>
    /// <remarks>
    /// Enforced at the single funnel every in-wave heal passes through (<c>SoloBattle.Heal</c>), against
    /// what actually LANDED — overhealing at full health does not spend the budget. Set it to
    /// <see cref="float.PositiveInfinity"/> to switch the ceiling off (the balance probe does, to
    /// print the "before" row).
    /// </remarks>
    public float MaxHealFractionPerWave { get; init; } = 0.40f;

    /// <summary>The live numbers.</summary>
    public static HealTuning Default { get; } = new();

    /// <summary>
    /// The numbers the playtest complained about — 50% leech, no ceiling. Kept as a named fixture so the
    /// balance probe can print before and after from the same code, and so the reason for the rework
    /// stays reproducible rather than remembered.
    /// </summary>
    public static HealTuning Legacy { get; } = new()
    {
        TransformationLeech = 0.50f,
        SiphonCeilingMultiplier = 1f,
        MaxHealFractionPerWave = float.PositiveInfinity,
    };

    /// <summary>
    /// The heal budget for a pool of <paramref name="maxHealth"/>, in whole health points — raised by
    /// <see cref="SiphonCeilingMultiplier"/> when the build carries SIPHON.
    /// </summary>
    public long BudgetFor(int maxHealth, bool siphon = false)
    {
        if (float.IsPositiveInfinity(MaxHealFractionPerWave)) return long.MaxValue;
        var fraction = MathF.Max(0f, MaxHealFractionPerWave) * (siphon ? MathF.Max(1f, SiphonCeilingMultiplier) : 1f);
        return (long)MathF.Round(Math.Max(0, maxHealth) * fraction);
    }

    /// <summary>The ceiling as the player reads it — "40%", or "60%" wearing SIPHON.</summary>
    public string CeilingText(bool siphon = false)
        => float.IsPositiveInfinity(MaxHealFractionPerWave)
            ? "no limit"
            : $"{MaxHealFractionPerWave * (siphon ? SiphonCeilingMultiplier : 1f):0%}";
}
