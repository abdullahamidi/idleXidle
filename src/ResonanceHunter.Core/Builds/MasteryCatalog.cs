using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;

namespace ResonanceHunter.Core.Builds;

/// <summary>The four demands the content makes, and therefore the four branches that answer them.</summary>
/// <remarks>
/// Named for the ANSWER, not the enemy. A branch called "Anti-Armour" would be a lookup table; a branch
/// called WEIGHT is a way of fighting that happens to beat armour, and can be wrong somewhere else.
/// </remarks>
public enum Branch
{
    /// <summary>Few, enormous hits. Answers Armoured. Opposed to <see cref="Spread"/>.</summary>
    Weight,

    /// <summary>Many targets. Answers Swarm. Opposed to <see cref="Weight"/>.</summary>
    Spread,

    /// <summary>Front-loaded damage. Answers Caster. Opposed to <see cref="Endure"/>.</summary>
    Tempo,

    /// <summary>Health, leech, mitigation. Answers Bruiser. Opposed to <see cref="Tempo"/>.</summary>
    Endure,
}

/// <summary>
/// The catalogue: the fixed shape of the skill tree, identical for every player.
/// </summary>
/// <remarks>
/// <para>
/// FOUR BRANCHES, ONE PER DEMAND. The previous tree had six arms, one per Form, which sounds like the same
/// idea and is not: Form is what a skill IS, and the content asks about what a build DOES. Six Form arms
/// meant six flavours of the same question and no way to answer "five creatures at once" except by
/// happening to have picked Aura. The branches map onto the archetype engine directly — Weight answers
/// Armoured, Spread answers Swarm, Tempo answers Caster, Endure answers Bruiser — so a player who reads
/// their post-run report knows which branch it is pointing at.
/// </para>
/// <para>
/// The pairs are OPPOSED. Weight and Spread trade hit size against target count on nearly every node;
/// Tempo and Endure trade front-loading against lasting. A build that walks one axis to its end is
/// excellent in one band and stalls in the band that asks the opposite question, and that stall is the
/// game.
/// </para>
/// <para>
/// <b>Rings and prices.</b> 4 minors at 1, 3 notables at 3, 2 greaters at 5, 1 mastery at 8 — a full
/// branch is 31. Four bridges at 6 and six Form specialisations at 6 bring the tree to 184 against
/// roughly 60 points at full current content, which is the design's one-third reachable invariant. Two
/// complete branches cost 62 and are therefore exactly, deliberately out of reach.
/// </para>
/// <para>
/// <b>No node is a bare multiplier.</b> Every entry below changes a shape — hit size, target count,
/// cooldown, threshold, trigger — or is an explicit trade. Even the connective minors move a shape rather
/// than a number, which is why they carry <see cref="SkillShape"/> and not <see cref="BuildMods"/>.
/// </para>
/// </remarks>
public static class MasteryCatalog
{
    public const string StartId = "start";

    /// <summary>Cost of a node at each ring. Index 0 is the free START.</summary>
    public static readonly IReadOnlyList<int> RingCost = new[] { 0, 1, 3, 5, 8 };

    /// <summary>What one bridge or one Form specialisation costs.</summary>
    public const int BridgeCost = 6;
    public const int SpecialisationCost = 6;

    public static IReadOnlyList<MasteryNode> Nodes { get; } = Build();

    public static MasteryNode? ById(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    /// <summary>Every point in the tree, if a player could somehow take all of it.</summary>
    public static int TotalCost => Nodes.Sum(n => n.Cost);

    /// <summary>What one complete branch costs — the design's affordability unit.</summary>
    public static int BranchCost(Branch branch)
        => Nodes.Where(n => n.Branch == branch && n.Kind is not MasteryKind.Bridge and not MasteryKind.Specialisation)
                .Sum(n => n.Cost);

    private static SkillShape S => SkillShape.None;

    private static IReadOnlyList<MasteryNode> Build()
    {
        var n = new List<MasteryNode>
        {
            new(StartId, MasteryKind.Start, Branch.Weight, 0, 0, "YOU", S, null, Array.Empty<string>()),
        };

        Weight(n);
        Spread(n);
        Tempo(n);
        Endure(n);
        Bridges(n);
        Specialisations(n);
        return n;
    }

    // ── WEIGHT — answers Armoured. Fewer, larger hits. ────────────────────────────────────────────

    private static void Weight(List<MasteryNode> n)
    {
        const Branch b = Branch.Weight;

        Minor(n, b, "heavy_hand", "HEAVY HAND — BIGGER HITS, SLOWER",
            S with { HitSize = 1.25f, SkillRate = 0.85f });
        Minor(n, b, "sharpened", "SHARPENED — HITS CUT 8 ARMOUR",
            S with { ArmourPenetration = 8f });
        Minor(n, b, "follow_through", "FOLLOW THROUGH — FIRST HIT ON A CREATURE +30%",
            S with { FirstHitMultiplier = 1.30f });
        Minor(n, b, "deliberate", "DELIBERATE — MUCH BIGGER HITS, MUCH SLOWER",
            S with { HitSize = 1.30f, SkillRate = 0.80f });

        // SUNDER strips armour permanently for the rest of the wave, so a Weight build gets STRONGER as an
        // Armoured wave goes on — the opposite of how flat mitigation normally works, and the reason the
        // branch beats Plated bands rather than merely surviving them.
        Notable(n, b, "sunder", "SUNDER — A HIT OVER 200 STRIPS 20 ARMOUR FOR THE WAVE",
            S with { SunderThreshold = 200f, SunderAmount = 20f },
            new[] { "heavy_hand", "sharpened", "follow_through", "deliberate" });
        Notable(n, b, "crush", "CRUSH — HITS 8x A CREATURE'S ARMOUR IGNORE HALF OF IT",
            S with { CrushArmourMultiple = 8f, ArmourIgnoreFraction = 0.5f },
            new[] { "heavy_hand", "sharpened", "follow_through", "deliberate" });
        Notable(n, b, "breaker", "BREAKER — HALF OF ANY OVERKILL CARRIES TO THE NEXT CREATURE",
            S with { OverkillCarry = 0.5f },
            new[] { "heavy_hand", "sharpened", "follow_through", "deliberate" });

        Greater(n, b, "monolith", "MONOLITH — HALF AS MANY HITS, EACH TWICE AS LARGE",
            S with { HitSize = 2f, SkillRate = 0.5f },
            new[] { "sunder", "crush", "breaker" });
        Greater(n, b, "siege", "SIEGE — +45% TO ARMOURED, -25% TO EVERYTHING ELSE",
            S with { VsArmouredBonus = 0.45f, VsOtherPenalty = 0.25f },
            new[] { "sunder", "crush", "breaker" });

        // THE BRANCH IN ONE NODE. Armour stops existing — and so does any build that delivers its damage
        // in small pieces, which is every Spread build in the game. It is the clearest statement in the
        // tree that the two axes are opposed.
        Mastery(n, b, "overwhelm", "OVERWHELM — HITS UNDER 60 DO NOTHING; HITS OVER 60 IGNORE ARMOUR",
            S with { OverwhelmFloor = 60f },
            new[] { "monolith", "siege" });
    }

    // ── SPREAD — answers Swarm. Target count and action economy. ──────────────────────────────────

    private static void Spread(List<MasteryNode> n)
    {
        const Branch b = Branch.Spread;

        Minor(n, b, "wide", "WIDE — PROJECTILE REACHES ONE MORE",
            S with { FormTargets = new Dictionary<Form, int> { [Form.Projectile] = 1 } });
        Minor(n, b, "diffuse", "DIFFUSE — AURA TICKS 25% FASTER",
            S with { SkillRate = 1.10f, HitSize = 0.95f });
        Minor(n, b, "quick_hands", "QUICK HANDS — COOLDOWNS -15%",
            S with { SkillRate = 1.18f });
        Minor(n, b, "ricochet", "RICOCHET — 30% OF HITS STRIKE A SECOND CREATURE FOR HALF",
            S with { RicochetChance = 0.30f, RicochetFraction = 0.5f });

        Notable(n, b, "chain", "CHAIN — EVERY HIT STRIKES ONE MORE CREATURE AT 50%",
            S with { ChainFraction = 0.5f },
            new[] { "wide", "diffuse", "quick_hands", "ricochet" });
        Notable(n, b, "cull", "CULL — +80% TO CREATURES UNDER 25% HEALTH",
            S with { CullThreshold = 0.25f, CullBonus = 0.80f },
            new[] { "wide", "diffuse", "quick_hands", "ricochet" });
        Notable(n, b, "swarmbane", "SWARMBANE — +8% DAMAGE PER LIVING CREATURE",
            S with { PerCreatureBonus = 0.08f },
            new[] { "wide", "diffuse", "quick_hands", "ricochet" });

        Greater(n, b, "cascade", "CASCADE — AFTER A KILL, THE NEXT SKILL STRIKES EVERYTHING",
            S with { CascadeOnKill = true },
            new[] { "chain", "cull", "swarmbane" });
        Greater(n, b, "dispersal", "DISPERSAL — ALL FORMS +1 TARGET, HITS -25%",
            S with { ExtraTargets = 1, HitSize = 0.75f },
            new[] { "chain", "cull", "swarmbane" });

        // Swarms evaporate. Anything with armour becomes nearly immune, because 40% of a small hit lands
        // under the flat mitigation floor — the exact mirror of what OVERWHELM does to Spread builds.
        Mastery(n, b, "everywhere", "EVERYWHERE — EVERY SKILL STRIKES EVERY CREATURE AT 40%",
            S with { StrikesEveryCreature = true, HitSize = 0.40f },
            new[] { "cascade", "dispersal" });
    }

    // ── TEMPO — answers Caster. Kill it before it acts. ───────────────────────────────────────────

    private static void Tempo(List<MasteryNode> n)
    {
        const Branch b = Branch.Tempo;

        Minor(n, b, "opener", "OPENER — +35% ON THE FIRST HIT AGAINST EACH CREATURE",
            S with { FirstHitMultiplier = 1.35f });
        Minor(n, b, "hasten", "HASTEN — +20% SKILL RATE",
            S with { SkillRate = 1.20f });
        Minor(n, b, "preparation", "PREPARATION — EVERY SKILL'S FIRST CAST IS FREE",
            S with { FreeOpeningCast = true });
        Minor(n, b, "focus", "FOCUS — +10% CRITICAL CHANCE",
            S with { BonusCritPercent = 10f });

        Notable(n, b, "alpha", "ALPHA — FIRST HIT x2.2, EVERY LATER HIT x0.75",
            S with { FirstHitMultiplier = 2.2f, LaterHitMultiplier = 0.75f },
            new[] { "opener", "hasten", "preparation", "focus" });
        Notable(n, b, "mark_mastery", "MARK MASTERY — THE WINDOW LASTS TWICE AS LONG AND HITS 40% HARDER",
            S with { MarkWindowMultiplier = 2f, MarkPowerBonus = 0.40f },
            new[] { "opener", "hasten", "preparation", "focus" });
        Notable(n, b, "interrupt", "INTERRUPT — DAMAGE IN THE RUN-UP TO AN ENEMY SWING +60%",
            S with { InterruptBonus = 0.60f },
            new[] { "opener", "hasten", "preparation", "focus" });

        Greater(n, b, "blitz", "BLITZ — COOLDOWNS -40%, HITS -25%",
            S with { SkillRate = 1.67f, HitSize = 0.75f },
            new[] { "alpha", "mark_mastery", "interrupt" });
        Greater(n, b, "assassinate", "ASSASSINATE — ONCE A WAVE, A CREATURE UNDER 40% DIES OUTRIGHT",
            S with { AssassinateThreshold = 0.40f },
            new[] { "alpha", "mark_mastery", "interrupt" });

        // Every wave becomes an opener. Bruisers, which cannot be killed in three seconds, become the wall
        // — which is what makes this the mastery that Endure exists to be the answer to.
        Mastery(n, b, "first_strike", "FIRST STRIKE — +180% FOR THREE SECONDS, -45% AFTER",
            S with { OpeningSeconds = 3f, OpeningBonus = 1.80f, AfterOpeningPenalty = 0.45f },
            new[] { "blitz", "assassinate" });
    }

    // ── ENDURE — answers Bruiser, and extends every other band. ───────────────────────────────────

    private static void Endure(List<MasteryNode> n)
    {
        const Branch b = Branch.Endure;

        Minor(n, b, "toughness", "TOUGHNESS — +20% MAXIMUM HEALTH",
            S with { MaxHealth = 1.20f });
        Minor(n, b, "leech", "LEECH — HEAL 2% OF DAMAGE DEALT",
            S with { Leech = 0.02f });
        Minor(n, b, "padding", "PADDING — EVERY BITE DEALS 6 LESS",
            S with { FlatDamageReduction = 6f });
        Minor(n, b, "second_wind", "SECOND WIND — HEAL 8% ON CLEARING A WAVE",
            S with { HealOnClear = 0.08f });

        // The one node in the game that buys an exception to "health does not regenerate between waves".
        Notable(n, b, "recovery", "RECOVERY — REGAIN 20% OF HEALTH BETWEEN WAVES",
            S with { BetweenWaveRegen = 0.20f },
            new[] { "toughness", "leech", "padding", "second_wind" });
        Notable(n, b, "absorb", "ABSORB — MITIGATION RISES AS HEALTH FALLS, TO -40%",
            S with { AbsorbAtLowHealth = 0.40f },
            new[] { "toughness", "leech", "padding", "second_wind" });
        Notable(n, b, "fortify", "FORTIFY — THE FIRST BITE OF EACH WAVE DEALS NOTHING",
            S with { FirstBiteFree = true },
            new[] { "toughness", "leech", "padding", "second_wind" });

        Greater(n, b, "bulwark", "BULWARK — DAMAGE TAKEN -35%, DAMAGE DEALT -20%",
            S with { DamageTaken = 0.65f, DamageDealt = 0.80f },
            new[] { "recovery", "absorb", "fortify" });
        Greater(n, b, "bastion", "BASTION — +1% DAMAGE PER 200 MAXIMUM HEALTH",
            S with { DamagePerMaxHealth = 0.01f / 200f },
            new[] { "recovery", "absorb", "fortify" });

        // The depth-pusher's node: it converts a health POOL into a per-wave ALLOWANCE, which is strictly
        // better the deeper you go and strictly worse in any single hard wave.
        Mastery(n, b, "endless", "ENDLESS — FULL HEALTH EVERY WAVE; MAXIMUM HEALTH HALVED",
            S with { FullHealBetweenWaves = true, MaxHealth = 0.5f },
            new[] { "bulwark", "bastion" });
    }

    // ── BRIDGES — hybridising, at a price that costs a mastery. ───────────────────────────────────

    private static void Bridges(List<MasteryNode> n)
    {
        Bridge(n, Branch.Weight, Branch.Tempo, "executioner",
            "EXECUTIONER — THE FIRST HIT ON EACH CREATURE CUTS 12 ARMOUR",
            S with { FirstHitMultiplier = 1.15f, ArmourPenetration = 12f });
        Bridge(n, Branch.Tempo, Branch.Spread, "volley",
            "VOLLEY — +1 TARGET AND -25% COOLDOWN, HITS -30%",
            S with { ExtraTargets = 1, SkillRate = 1.33f, HitSize = 0.70f });
        Bridge(n, Branch.Spread, Branch.Endure, "feedback",
            "FEEDBACK — HEAL 1.5% OF MAXIMUM PER CREATURE STRUCK",
            S with { HealPerTargetStruck = 0.015f });
        Bridge(n, Branch.Endure, Branch.Weight, "anchor",
            "ANCHOR — +1% HIT SIZE PER 150 MAXIMUM HEALTH",
            S with { HitSizePerMaxHealth = 0.01f / 150f });
    }

    // ── FORM SPECIALISATIONS — the only triggers this tree grants. ────────────────────────────────

    /// <summary>
    /// One per Form, hanging off the axis its Form belongs to.
    /// </summary>
    /// <remarks>
    /// This is how the skill tree stops giving away what the trait tree sells. The old tree handed out
    /// general triggers for free while the Dust tree charged a permanent price for the same ones. Here the
    /// skill tree grants only the six FORM-COMBO triggers — each dead weight without its Form, so it is a
    /// specialisation rather than a gift — and every general trigger (Echo, Bloodlust, Undying, Venom,
    /// Splinter, Harvest, Desperation, Zeal, NoHealing) belongs to the trait tree alone.
    /// </remarks>
    private static void Specialisations(List<MasteryNode> n)
    {
        Spec(n, Branch.Weight, Form.Strike, "spec_strike", "STRIKE SPECIALIST — EXECUTE WEAKENED FOES",
            BuildTrigger.Execute, S with { CullThreshold = 0.35f, CullBonus = 0.40f });
        Spec(n, Branch.Weight, Form.Trap, "spec_trap", "TRAP SPECIALIST — TRAPS RE-ARM ON BEING HIT",
            BuildTrigger.Coiled, S);
        Spec(n, Branch.Spread, Form.Projectile, "spec_projectile", "VOLLEY SPECIALIST — ONE MORE SHOT, ONE MORE TARGET",
            BuildTrigger.Overdraw, S with { FormTargets = new Dictionary<Form, int> { [Form.Projectile] = 1 } });
        Spec(n, Branch.Spread, Form.Aura, "spec_aura", "AURA SPECIALIST — TICKS 30% FASTER",
            BuildTrigger.Radiance, S);
        Spec(n, Branch.Tempo, Form.Mark, "spec_mark", "MARK SPECIALIST — THE WINDOW STRETCHES",
            BuildTrigger.Linger, S with { MarkWindowMultiplier = 1.3f });
        Spec(n, Branch.Endure, Form.Transformation, "spec_transformation", "MORPH SPECIALIST — LEECH DOUBLED",
            BuildTrigger.Siphon, S);
    }

    // ── Construction helpers. Ring decides cost, so a node cannot be mispriced by hand. ───────────

    private static void Minor(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape)
        => n.Add(new(id, MasteryKind.Minor, b, 1, RingCost[1], label, shape, null, new[] { StartId }));

    private static void Notable(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape,
                                string[] prereqs)
        => n.Add(new(id, MasteryKind.Notable, b, 2, RingCost[2], label, shape, null, prereqs));

    private static void Greater(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape,
                                string[] prereqs)
        => n.Add(new(id, MasteryKind.Greater, b, 3, RingCost[3], label, shape, null, prereqs));

    private static void Mastery(List<MasteryNode> n, Branch b, string id, string label, SkillShape shape,
                                string[] prereqs)
        => n.Add(new(id, MasteryKind.Mastery, b, 4, RingCost[4], label, shape, null, prereqs));

    /// <summary>A bridge needs ring 2 of BOTH neighbours — hybridising is earned, not splashed.</summary>
    private static void Bridge(List<MasteryNode> n, Branch a, Branch c, string id, string label, SkillShape shape)
        => n.Add(new(id, MasteryKind.Bridge, a, 2, BridgeCost, label, shape, null, NotablesOf(n, a).ToArray())
        {
            Link = c,
            SecondPrereqs = NotablesOf(n, c).ToArray(),
        });

    private static void Spec(List<MasteryNode> n, Branch b, Form form, string id, string label,
                             BuildTrigger grant, SkillShape shape)
        => n.Add(new(id, MasteryKind.Specialisation, b, 3, SpecialisationCost, label, shape, grant,
                     NotablesOf(n, b).ToArray()) { Form = form });

    private static IEnumerable<string> NotablesOf(List<MasteryNode> n, Branch b)
        => n.Where(x => x.Branch == b && x.Kind == MasteryKind.Notable).Select(x => x.Id);
}
