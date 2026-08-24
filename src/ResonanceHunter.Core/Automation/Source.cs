namespace ResonanceHunter.Core.Automation;

/// <summary>
/// The six elemental Sources — the ELEMENT vocabulary of the whole game.
/// </summary>
/// <remarks>
/// This enum used to live in <c>Creature.cs</c>, but it was never really the creature subsystem's:
/// abilities, builds, enemies, regions, items and chests all speak Source. When the creature /
/// evolution / automation subsystem was retired (2026-08-24), the enum moved to its own file so the
/// rest of the game keeps its element language — same namespace, same name, no caller changed.
/// </remarks>
public enum Source { Body, Mind, Nature, Machine, Shadow, Spirit }
