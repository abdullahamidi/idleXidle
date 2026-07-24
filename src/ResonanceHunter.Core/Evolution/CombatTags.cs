using ResonanceHunter.Core.Economy;

namespace ResonanceHunter.Core.Evolution;

/// <summary>
/// The vocabulary shared by whatever RECORDS progress and whatever REQUIRES it.
/// </summary>
/// <remarks>
/// <para>
/// Both sides of an evolution condition used to be free-typed strings, and both sides were wrong. The
/// BULWARK MOSS branch required the trait <c>"ward"</c> while the enum member is <c>Warding</c> — so
/// even a player who ground out all forty job ticks with the right charm equipped would have been
/// refused by a string mismatch, silently, forever. Nothing could catch that: both spellings compiled.
/// </para>
/// <para>
/// Constants do not make the two sides agree by magic, but they make disagreement a COMPILE error
/// instead of a player's wasted afternoon, and <see cref="TraitTag"/> derives the trait's name from
/// the enum rather than restating it — so renaming a trait cannot silently orphan a branch again.
/// </para>
/// </remarks>
public static class CombatTags
{
    /// <summary>
    /// A boss wave felled. The auto-battler's replacement for <c>part_break</c>.
    /// </summary>
    /// <remarks>
    /// Part-breaking was manual combat's whole skill expression, deleted with it — its only recorder
    /// had zero callers, so the branch that required 8 of them was unreachable for every player who
    /// ever ran the game. A felled boss is the honest equivalent: a deliberate, dangerous thing the
    /// player DID, rather than time they let pass.
    /// </remarks>
    public const string BossFelled = "boss_felled";

    /// <summary>A wave cleared. Cheap and constant — texture, never a gate on its own.</summary>
    public const string WaveCleared = "wave_cleared";

    /// <summary>
    /// A boss chest cracked open at the Forge. The CRAFTER path's earn.
    /// </summary>
    /// <remarks>
    /// It replaced <see cref="Banked"/>: the CRAFTER branch used to want "runs banked", and the idle loop
    /// deleted banking — so that branch was unreachable for anyone until this. Opening chests is the honest
    /// craft-flavoured equivalent — the crafter is the one who works the loot — and, unlike a boss felled,
    /// it is a deliberate SEPARATE act (you can hoard chests), so it stays distinct from the ATTACKER path.
    /// </remarks>
    public const string ChestOpened = "chest_opened";

    /// <summary>DEAD in the solo model — banking was removed. Kept only for the dormant squad recorder.</summary>
    public const string Banked = "banked";

    /// <summary>The canonical tag for a worn trait. Derived from the enum, never retyped.</summary>
    public static string TraitTag(GearTrait trait) => trait.ToString();

    /// <summary>The WARDING trait, as an evolution condition would name it.</summary>
    public static string WardingTrait => TraitTag(GearTrait.Warding);
}
