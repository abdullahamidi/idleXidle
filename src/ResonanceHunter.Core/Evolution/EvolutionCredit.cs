using System;
using System.Collections.Generic;
using ResonanceHunter.Core.Automation;

namespace ResonanceHunter.Core.Evolution;

/// <summary>
/// Credits the champion's expedition combat to the warren, so the combat-gated evolution branches have
/// their inputs PRODUCED.
/// </summary>
/// <remarks>
/// <para>
/// The signature bug of this codebase, one more time. <see cref="EvolutionTrees"/> asks every creature
/// to "FELL 4 BOSSES" (ATTACKER) or "CLEAR 40 WAVES" (SUPPORT); <see cref="EvolutionTree.EdgeSatisfied"/>
/// reads <see cref="EvolutionProgress.CombatTally"/>; the save format persists it. And the ONLY thing
/// that ever wrote it was the squad's <c>ExpeditionRecord</c> — which went dormant when the squad was
/// replaced by a single champion. So <c>CombatTally</c> stayed empty for every creature in every save,
/// and the ATTACKER and SUPPORT branches could not fire for anyone. It is the exact shape of the
/// <c>HealthyWorkTicks</c> starvation that <see cref="RegionAutomation"/> already had to fix once.
/// </para>
/// <para>
/// The champion does the fighting now, and a felled boss is "a thing the PLAYER did"
/// (<see cref="CombatTags.BossFelled"/>) — so its victories are the whole warren's, and this credits
/// EVERY owned creature, not just the ones staffing a farm. There is no health gate, because the creature
/// is not the one swinging; what still differentiates the branches per-creature is MATERIALS, fed by hand.
/// </para>
/// <para>
/// (History: the CRAFTER branch once asked for a "banked run", a mechanic the idle loop deleted, and was
/// unreachable. It was re-themed to <see cref="CombatTags.ChestOpened"/> and is credited from the reward
/// loop now — all five branches have a live producer, except see the Defender note in EvolutionTrees.)
/// </para>
/// </remarks>
public static class EvolutionCredit
{
    /// <summary>
    /// Credit one cleared wave to every creature that has begun evolving: a <see cref="CombatTags.WaveCleared"/>
    /// for each, and a <see cref="CombatTags.BossFelled"/> as well when the wave was a boss.
    /// </summary>
    public static void CreditWave(IEnumerable<Creature> roster, bool wasBoss)
    {
        ArgumentNullException.ThrowIfNull(roster);

        foreach (var c in roster)
        {
            // A creature that has not begun evolving has no tally to write — skip it, exactly as the
            // work-tick crediting does.
            if (c.Evolution is not { } progress) continue;

            progress.RecordCombat(CombatTags.WaveCleared);
            if (wasBoss) progress.RecordCombat(CombatTags.BossFelled);
        }
    }

    /// <summary>Credit a cracked-open boss chest to every creature — the CRAFTER path's input.</summary>
    public static void CreditChestOpened(IEnumerable<Creature> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        foreach (var c in roster)
            c.Evolution?.RecordCombat(CombatTags.ChestOpened);
    }
}
