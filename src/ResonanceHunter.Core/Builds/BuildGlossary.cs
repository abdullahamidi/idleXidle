using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;


namespace ResonanceHunter.Core.Builds;

/// <summary>
/// What every Form and every Source actually DOES, in the player's words.
/// </summary>
/// <remarks>
/// <para>
/// The Weave screen asks a player to pick four skills out of six Forms crossed with six Sources, and
/// told them the names and nothing else. The playtest said so plainly: <i>"I do not know what the
/// skills do, or what difference the ones I picked make."</i> A build game whose central choice is
/// unlabelled is not a build game — it is a slot machine with a nice frame.
/// </para>
/// <para>
/// Every line here is derived from the rule it describes rather than written beside it. The cooldown
/// text reads <see cref="FormBehaviour.BaseCooldownMs"/>, the reach text reads
/// <see cref="FormBehaviour.Targets"/>, the Mark window reads its own constants. That is deliberate and
/// it is the whole reason this lives in Core next to the rules: a description hand-written in a screen
/// is a second copy of the design, and the copy is the one that goes stale. Retune a cooldown and this
/// text retunes with it.
/// </para>
/// </remarks>
public static class BuildGlossary
{
    /// <summary>One line: what this Form IS. The identity, not the numbers.</summary>
    public static string FormHeadline(Form form) => form switch
    {
        Form.Strike => "ONE HEAVY BLOW",
        Form.Projectile => "MANY LIGHT SHOTS",
        Form.Aura => "A FIELD THAT NEVER STOPS",
        Form.Trap => "PUNISHES BEING BITTEN",
        Form.Mark => "MAKES EVERYTHING ELSE HURT",
        _ => "HITS AND HEALS YOU BACK",
    };

    /// <summary>
    /// The rule that makes this Form different from the others, stated with its real numbers.
    /// </summary>
    /// <remarks>
    /// Each of these is the ONE sentence that decides whether a Form belongs in a build. A player who
    /// reads "reaches every creature at once" understands why Aura clears a swarm and dies to a boss
    /// without having to lose a run to find out.
    /// </remarks>
    public static string FormRule(Form form)
    {
        var cd = FormBehaviour.BaseCooldownMs(form) / 1000f;

        return form switch
        {
            Form.Aura =>
                $"No cooldown. Ticks every {FormBehaviour.AuraTickMs / 1000f:0.0}s for the whole fight, "
                + "and reaches EVERY creature at once — the smallest hit in the game, landing everywhere.",

            Form.Trap =>
                $"Every {cd:0.#}s, but ONLY pays when the enemy attacks you. Against something that "
                + "never swings it does nothing at all.",

            Form.Mark =>
                $"Deals NO damage. Every {cd:0.#}s it opens a {FormBehaviour.MarkWindowMs / 1000f:0.0}s "
                + $"window in which everything else you do hits {FormBehaviour.MarkMultiplier:0.0}x harder. "
                + "Worthless alone; multiplies a build that already works.",

            Form.Projectile =>
                $"Every {cd:0.#}s — roughly twice as often as a Strike — and reaches "
                + $"{FormBehaviour.Targets(form)} creatures. Volume instead of weight.",

            Form.Transformation =>
                $"Every {cd:0.#}s, and returns {FormBehaviour.TransformationLeech * 100f:0}% of the "
                + "damage it deals as health. The only Form that keeps you alive.",

            _ =>
                $"Every {cd:0.#}s, one creature, the largest single hit of any Form. "
                + "Armour eats a fixed amount per hit, so one big blow loses far less to it than many small ones.",
        };
    }

    /// <summary>What a Source is for, and the shape of its matchup.</summary>
    /// <remarks>
    /// A Source is the only lever whose value depends on WHERE you are rather than on your build, so
    /// the line has to say that. The strong/weak pairs come from
    /// <see cref="Weaving.SourceEffectiveness"/> rather than being listed by hand.
    /// </remarks>
    public static string SourceRule(Source source, WeavingTuning? tuning = null)
    {
        tuning ??= WeavingTuning.Default;

        var strong = StrongAgainst(source);
        return $"x{tuning.StrongMultiplier:0.00} against {Name(strong.A)} and {Name(strong.B)}. "
               + $"Each region has its own element — match it and every skill of this Source hits harder.";
    }

    /// <summary>The matchup as ONE short line, for a panel with a single row to spare.</summary>
    /// <remarks>
    /// The long form explains WHY a Source matters; this one is for where there is only room to say
    /// WHICH. Both are generated from the same pair, so they can never disagree about the matchup.
    /// </remarks>
    public static string SourceLine(Source source, WeavingTuning? tuning = null)
    {
        tuning ??= WeavingTuning.Default;
        var (a, b) = StrongAgainst(source);
        return $"x{tuning.StrongMultiplier:0.0} vs {Name(a)} and {Name(b)} — match the region's element";
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

    /// <summary>
    /// How a skill of this Source and Form reads as one line, for a slot the player has already filled.
    /// </summary>
    public static string SkillSummary(Source source, Form form)
        => $"{Name(source)} {form.ToString().ToUpperInvariant()} — {FormHeadline(form)}";
}
