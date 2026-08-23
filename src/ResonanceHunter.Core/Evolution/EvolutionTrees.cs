using System.Collections.Generic;
using ResonanceHunter.Core.Automation;

namespace ResonanceHunter.Core.Evolution;

/// <summary>
/// One branching tree per Source — so a creature's ELEMENT shapes what it can become.
/// </summary>
/// <remarks>
/// <para>
/// Evolution reached exactly one line of play. <c>AutomationScreen</c> handed every creature the
/// NatureTree regardless of its Source, and only if <c>Role == Attacker</c> — so a Machine Support
/// had no tree at all, and the sole reachable outcome in the whole game was Whelp → Sporeling. The
/// design asks for evolutions "influenced by their element, equipped items, assigned job, combat
/// behavior, and consumed materials", and four of those five had no data source at all.
/// </para>
/// <para>
/// <b>Every Source gets the same five-branch SHAPE, and its own names.</b> The shape is the design —
/// five roles, each demanding a genuinely different kind of play — and duplicating it six times by
/// hand would guarantee the six drift apart. The names are hand-authored rather than generated,
/// because "MACHINE ATTACKER MK2" is not a creature anyone will remember.
/// </para>
/// <para>
/// Each branch asks for something you cannot buy with the same currency as the others: bosses felled
/// (deliberate danger), job ticks with a specific charm (patience plus a build), waves survived
/// (endurance), chests opened (working the loot), materials (pure feeding). That is what stops
/// evolution from being a single "grind more" bar with five labels on it.
/// </para>
/// </remarks>
public static class EvolutionTrees
{
    /// <summary>
    /// Names per Source, in Role order: root, Attacker, Defender, Support, Crafter, Producer.
    /// </summary>
    private static readonly Dictionary<Source, string[]> Names = new()
    {
        [Source.Nature] = new[] { "VERDANT WHELP", "THORNSTALKER", "BULWARK MOSS", "MOSSLING", "SAPWRIGHT", "SPORELING" },
        [Source.Machine] = new[] { "SCRAP WHELP", "RIVET WARDEN", "PLATED BULWARK", "MEND DRONE", "COGWRIGHT", "BOILER ENGINE" },
        [Source.Shadow] = new[] { "DUSK WHELP", "NIGHT STALKER", "GLOOM WARD", "PALE WISP", "SILK WEAVER", "SPORE OF NIGHT" },
        [Source.Body] = new[] { "RAW WHELP", "SINEW BRUTE", "BONE WALL", "PULSE KIN", "MARROW SMITH", "BROOD MOTHER" },
        [Source.Mind] = new[] { "STILL WHELP", "LANCE OF THOUGHT", "CALM AEGIS", "QUIET CHORUS", "SCHEMA WRIGHT", "IDEA BLOOM" },
        [Source.Spirit] = new[] { "FAINT WHELP", "ECHO FANG", "VIGIL SHADE", "SOLACE SHADE", "RITE KEEPER", "EMBER FONT" },
    };

    public static string RootIdFor(Source source) => $"{source.ToString().ToLowerInvariant()}_whelp";

    /// <summary>
    /// The tree for a Source. Every creature has one, whatever its Role.
    /// </summary>
    /// <remarks>
    /// A creature whose Role is already the branch's target still evolves — it gets the node's
    /// PowerTierBonus. Evolution is growth, not just reassignment.
    /// </remarks>
    public static EvolutionTree For(Source source)
    {
        var n = Names.TryGetValue(source, out var found) ? found : Names[Source.Nature];
        var slug = source.ToString().ToLowerInvariant();
        var root = RootIdFor(source);

        return new EvolutionTree(root, new[]
        {
            new EvolutionNode
            {
                Id = root,
                DisplayName = n[0],
                Role = Role.Attacker,
                Children =
                {
                    // ATTACKER — take it to war and fell bosses. Deliberate danger, not time spent.
                    new EvolutionEdge
                    {
                        TargetNodeId = $"{slug}_atk", BranchPriority = 0,
                        MaterialThreshold = 30,
                        CombatTag = CombatTags.BossFelled, CombatThreshold = 4,
                        Hint = $"FEED 30, KILL 4 BOSSES -> {n[1]} (ATTACKER)",
                    },
                    // DEFENDER — the labourer that endures: it must WORK the farm, tick after tick.
                    // The old requirement also demanded a WARDING charm equipped ON THE CREATURE — but the
                    // solo game has no way to equip a creature, so EquippedTrait was never written and this
                    // whole branch was unreachable for every player (a consumer with no producer, the
                    // codebase's signature bug). Dropped to the job-tick condition, which farming DOES produce,
                    // so a diligent worker can finally become a Defender.
                    new EvolutionEdge
                    {
                        TargetNodeId = $"{slug}_def", BranchPriority = 1,
                        MaterialThreshold = 20,
                        JobTicksThreshold = 40,
                        Hint = $"FEED 20, DO 40 JOBS -> {n[2]} (DEFENDER)",
                    },
                    // SUPPORT — endurance. Survive a great many waves, whatever the outcome.
                    new EvolutionEdge
                    {
                        TargetNodeId = $"{slug}_sup", BranchPriority = 2,
                        MaterialThreshold = 25,
                        CombatTag = CombatTags.WaveCleared, CombatThreshold = 40,
                        Hint = $"FEED 25, CLEAR 40 WAVES -> {n[3]} (SUPPORT)",
                    },
                    // CRAFTER — working the loot. Crack open boss chests rather than hoard them.
                    new EvolutionEdge
                    {
                        TargetNodeId = $"{slug}_crf", BranchPriority = 3,
                        MaterialThreshold = 45,
                        CombatTag = CombatTags.ChestOpened, CombatThreshold = 5,
                        Hint = $"FEED 45, OPEN 5 CHESTS -> {n[4]} (CRAFTER)",
                    },
                    // PRODUCER — the patient path. Always reachable, never optimal.
                    new EvolutionEdge
                    {
                        TargetNodeId = $"{slug}_prd", BranchPriority = 4,
                        MaterialThreshold = 60,
                        Hint = $"FEED 60 MATERIALS -> {n[5]} (PRODUCER)",
                    },
                },
            },

            new EvolutionNode { Id = $"{slug}_atk", DisplayName = n[1], Role = Role.Attacker, PowerTierBonus = 3 },
            new EvolutionNode { Id = $"{slug}_def", DisplayName = n[2], Role = Role.Defender, PowerTierBonus = 3 },
            new EvolutionNode { Id = $"{slug}_sup", DisplayName = n[3], Role = Role.Support, PowerTierBonus = 2 },
            new EvolutionNode { Id = $"{slug}_crf", DisplayName = n[4], Role = Role.Crafter, PowerTierBonus = 2 },
            new EvolutionNode { Id = $"{slug}_prd", DisplayName = n[5], Role = Role.Producer, PowerTierBonus = 2 },
        });
    }
}
