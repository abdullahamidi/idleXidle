using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Sources;


namespace IdleXIdle.Core.Builds;

/// <summary>
/// What every Source actually DOES, in the player's words. (A SKILL's own sentence lives on the
/// catalogue — <see cref="SkillDef.Line"/> — since the skills stopped being composed from Forms.)
/// </summary>
/// <remarks>
/// <para>
/// The Weave screen asks a player to pick four skills out of six Forms crossed with six Sources, and
/// told them the names and nothing else. The playtest said so plainly: <i>"I do not know what the
/// skills do, or what difference the ones I picked make."</i> A build game whose central choice is
/// unlabelled is not a build game — it is a slot machine with a nice frame.
/// </para>
/// <para>
/// Every line here is derived from the rule it describes rather than written beside it — the
/// matchup multiplier reads <see cref="SourceMatchup.Strong"/>, the signatures read the sim's own
/// constants. That is deliberate and it is the whole reason this lives in Core next to the rules: a
/// description hand-written in a screen is a second copy of the design, and the copy is the one
/// that goes stale. Retune a number and this text retunes with it.
/// </para>
/// </remarks>
public static class BuildGlossary
{
    /// <summary>
    /// SHIELD — the one word the game uses for the resource, and the one sentence that explains it.
    /// </summary>
    /// <remarks>
    /// The resource is called SHIELD everywhere a player can read it. PLATING, BANKED and OVERGROWTH
    /// are the names of things that GRANT it — a capstone and a variation may have their own flavour,
    /// the resource may not, because a player who has learned one word should never have to discover
    /// that "barrier" and "ward" were the same bar. Written here rather than in a screen so the game
    /// has exactly one copy of it (see the class remarks).
    /// </remarks>
    public const string ShieldWord = "SHIELD";

    /// <summary>The sentence shown the first time SHIELD appears, and in the glossary after that.</summary>
    public const string ShieldRule = "SHIELD absorbs incoming damage before HEALTH.";

    /// <summary>What a Source is for, and the shape of its matchup.</summary>
    /// <remarks>
    /// A Source is the only lever whose value depends on WHERE you are rather than on your build, so
    /// the line has to say that. The strong/weak pairs come from
    /// <see cref="SourceMatchup.Effectiveness"/> rather than being listed by hand.
    /// </remarks>
    public static string SourceRule(Source source)
    {
        var strong = StrongAgainst(source);
        return $"{Signature(source)}. "
               + $"x{SourceMatchup.Strong:0.00} against {Name(strong.A)} and {Name(strong.B)} — "
               + $"match the region's element for a nudge; the signature is the identity.";
    }

    /// <summary>
    /// The Source's SIGNATURE — the one mechanical thing that fires whenever its skills land.
    /// </summary>
    /// <remarks>
    /// Numbers are formatted from the sim's own constants, so a retune can never leave a stale
    /// promise here. This is the Hatsu layer: what your element DOES, not what it counters.
    /// </remarks>
    public static string Signature(Source source) => source switch
    {
        Source.Body => $"Hits leave a WOUND — every skill hits a wounded foe +{SoloBattle.SignatureWoundPerStack * 100:0}% per wound ({SoloBattle.SignatureWoundMaxStacks} max)",
        Source.Shadow => $"+{SoloBattle.SignatureShadowBonus * 100:0}% against foes below half health",
        Source.Mind => "Each cast stretches an open MARK window a little longer",
        Source.Nature => $"Its skills heal you for {SoloBattle.SignatureNatureLeech * 100:0}% of the damage they deal",
        Source.Machine => "Every hit bends the foe's armour down, for the rest of the wave",
        _ => $"After it casts, your next skill of any OTHER Source hits +{SoloBattle.SignatureSpiritBonus * 100:0}%",
    };

    /// <summary>
    /// The one rule every heal in the game obeys, in the player's words — read from
    /// <see cref="HealTuning.Default"/> so a retune retunes the sentence.
    /// </summary>
    /// <remarks>
    /// Shown on the Transformation card because that is where a player first meets healing, but the
    /// rule is about the BUILD: leech nodes, the Nature signature and SECOND WIND all draw from the
    /// same per-wave allowance. It is stated once rather than on every node so the nodes stay short.
    /// </remarks>
    public static string HealCeilingRule(HealTuning? tuning = null)
    {
        tuning ??= HealTuning.Default;
        return $"Skills can restore at most {tuning.CeilingText()} of your health in one wave"
               + $" ({tuning.CeilingText(siphon: true)} with SIPHON).";
    }

    /// <summary>The matchup as ONE short line, for a panel with a single row to spare.</summary>
    /// <remarks>
    /// The long form explains WHY a Source matters; this one is for where there is only room to say
    /// WHICH. Both are generated from the same pair, so they can never disagree about the matchup.
    /// </remarks>
    public static string SourceLine(Source source)
        => Signature(source);

    /// <summary>The matchup as ONE short line — the second breath, after the signature.</summary>
    public static string MatchupLine(Source source)
    {
        var (a, b) = StrongAgainst(source);
        return $"x{SourceMatchup.Strong:0.00} vs {Name(a)} and {Name(b)} — match the region's element";
    }

    /// <summary>The two Sources this one is strong against — read from the matchup rule, never listed.</summary>
    public static (Source A, Source B) StrongAgainst(Source source)
    {
        // SourceEffectiveness makes an attacker strong against the next two in the ring.
        var i = (int)source;
        return ((Source)((i + 1) % 6), (Source)((i + 2) % 6));
    }

    /// <summary>The two Sources this one is weak against.</summary>
    public static (Source A, Source B) WeakAgainst(Source source)
    {
        var i = (int)source;
        return ((Source)((i + 4) % 6), (Source)((i + 5) % 6));
    }

    private static string Name(Source s) => s.ToString().ToUpperInvariant();
}
