namespace IdleXIdle.Core.Sources;

/// <summary>
/// The Source matchup ring — how a skill's Source fares against a creature's.
/// </summary>
/// <remarks>
/// <para>
/// A circulant cycle over the enum order: every Source is strong against the next two and weak
/// against the previous two, so each has exactly two strengths and two weaknesses and no Source
/// dominates. The table is antisymmetric by construction rather than a hand-authored grid someone
/// has to keep balanced.
/// </para>
/// <para>
/// The multipliers are deliberately SMALL (they were 1.5/0.667 once): at ×1.5 the correct play was
/// always "swap to the counter", which reduced the deepest-looking axis to a colour chart. At ×1.15
/// the matchup is a travel nudge — where do I hunt — and the Source SIGNATURES are the identity.
/// The playtest asked for builds you love, not counters you obey.
/// </para>
/// </remarks>
public static class SourceMatchup
{
    /// <summary>What a strong matchup multiplies a hit by.</summary>
    public const float Strong = 1.15f;

    /// <summary>What a weak matchup multiplies a hit by.</summary>
    public const float Weak = 0.87f;

    /// <summary>The attacker's multiplier against the target — ×Strong, ×Weak, or 1.</summary>
    public static float Effectiveness(Source attacker, Source target)
    {
        var delta = (((int)target - (int)attacker) % 6 + 6) % 6;
        return delta switch
        {
            1 or 2 => Strong,   // strong against the next two on the ring
            4 or 5 => Weak,     // weak against the previous two
            _ => 1.0f,          // self, and the opposite
        };
    }
}
