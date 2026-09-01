using System;

namespace IdleXIdle.Core.Encounters;

/// <summary>
/// How much harder each region is than the one before it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This was <c>1 + 0.35 * regionIndex</c>, inline in Game1, and that linear form is what let a
/// player conquer the fourth map without equipping anything.</b> Playtest, verbatim: <i>"itemsiz bir
/// şekilde 4. mapi tamamladım hiçbir şey yapmadan"</i>.
/// </para>
/// <para>
/// The failure is visible the moment the multipliers are written out. Linear gives 1.00, 1.35, 1.70,
/// 2.05, 2.40 — so the steps a player FEELS are +35%, +26%, +21%, +17%, +15%. <b>The ladder gets
/// flatter the higher you climb.</b> Player power does the opposite: ranks, keystones and gear all
/// multiply, so by the fourth region the champion is compounding against a difficulty curve that has
/// run out of slope. The probe caught it exactly — regions 2 and 3 fell at the same 80 ranks of
/// training, which is another way of saying the step between them cost nothing.
/// </para>
/// <para>
/// So the ladder is GEOMETRIC now: every region is the same multiple of the last, and the step a
/// player feels is the same one every time. That is the only shape that holds a constant challenge
/// against multiplicative growth, and it is the shape the rest of this codebase already uses for
/// anything that must not run away.
/// </para>
/// </remarks>
public static class RegionLadder
{
    /// <summary>
    /// How much tougher each region is than its predecessor, in health.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chosen against the attrition probe, not by feel: it is the smallest step at which each region on
    /// the chain demands strictly more training than the one before it. Lower and two neighbouring
    /// regions collapse into one difficulty, which is the bug this replaced. Much higher and the world's
    /// back half becomes a wall a player meets with no warning.
    /// </para>
    /// <para>
    /// Note this is smaller than the linear form's FIRST step (+35%) and far larger than its last
    /// (+15%). The change is not "the game is harder" — it is "the game stops getting easier".
    /// </para>
    /// </remarks>
    public const float HealthStep = 1.62f;

    /// <summary>
    /// The damage step. Deliberately gentler than health.
    /// </summary>
    /// <remarks>
    /// Enemy damage decides how FAST a losing run ends; enemy health decides WHETHER it is losing. Tying
    /// them to the same multiplier means a region you are underpowered for kills you before you have
    /// read anything, which turns a difficulty gate into a slot machine. Health gates; damage paces.
    /// </remarks>
    public const float DamageStep = 1.34f;

    /// <summary>Health multiplier for the region at this position on the chain (0 = the first region).</summary>
    public static float Health(int regionIndex) => MathF.Pow(HealthStep, Math.Max(0, regionIndex));

    /// <summary>Damage multiplier for the region at this position on the chain.</summary>
    public static float Damage(int regionIndex) => MathF.Pow(DamageStep, Math.Max(0, regionIndex));

    /// <summary>
    /// The relative step from one region to the next, as a player would describe it ("+62% tougher").
    /// </summary>
    /// <remarks>
    /// Exists so the map screen can state the ask, and so a test can assert the steps are all equal
    /// without re-deriving the curve — the drift this codebase keeps catching is a caption that stopped
    /// matching the rule it describes.
    /// </remarks>
    public static float StepPercent => (HealthStep - 1f) * 100f;
}
