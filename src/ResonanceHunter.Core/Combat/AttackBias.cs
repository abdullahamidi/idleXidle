namespace ResonanceHunter.Core.Combat;

/// <summary>
/// A region's combat CHARACTER — how its enemies bite. Read by the live single-champion fight
/// (<see cref="ResonanceHunter.Core.Builds.SoloExpedition"/>) to bend the enemy's tempo: HEAVY is slow and
/// hard, FAST is a quick flurry, BALANCED is the baseline. See <c>RegionDefinition.CombatBias</c>.
/// </summary>
/// <remarks>
/// This enum used to live in <c>CreatureAttacks.cs</c> alongside the retired manual-combat attack model. It
/// was the one live thing in that file, so it was lifted out here when the rest was deleted.
/// </remarks>
public enum AttackBias { Balanced, Heavy, Fast }
