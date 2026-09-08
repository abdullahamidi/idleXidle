using IdleXIdle.Core.Economy;

namespace IdleXIdle.Game;

/// <summary>
/// THE PLAYER'S WORD FOR EACH TRAINED STAT, and the one sentence that says what it is.
/// </summary>
/// <remarks>
/// <para>
/// The nine <see cref="HunterStat"/> members are internal names — <c>AttackPower</c>, <c>Engineering</c>,
/// <c>Guile</c> — and the player's names are MIGHT, TEMPO and GUILE. The words lived inside the Training
/// screen, so every other surface either printed the enum ("+3 ENGINEERING" on two Mastery nodes,
/// "+4 ATTACKPOWER" in the Mastery inspector) or said nothing at all (2026-09-08 release sweep).
/// </para>
/// <para>
/// One table, read by Training and by Mastery, so a stat is the same word wherever a player meets it and
/// its meaning travels with it. ENGINEERING is the clearest case: nothing in the game has ever shown that
/// word to a player except two Mastery nodes, and the stat it names is the one Training calls TEMPO.
/// </para>
/// </remarks>
public static class HunterStatWords
{
    /// <summary>The stat's player-facing name, in the game's own vocabulary.</summary>
    public static string Word(HunterStat s) => s switch
    {
        HunterStat.AttackPower => "MIGHT",
        HunterStat.ResonanceAffinity => "RESONANCE",
        HunterStat.CriticalChance => "CRITICAL",
        HunterStat.Focus => "FOCUS",
        HunterStat.MaxHealth => "HEALTH",
        HunterStat.Defense => "DEFENSE",
        HunterStat.Vitality => "VITALITY",
        HunterStat.Engineering => "TEMPO",
        _ => "GUILE",
    };

    /// <summary>One line saying what the stat is — the answer to "what changes if I buy this?".</summary>
    public static string Definition(HunterStat s) => s switch
    {
        HunterStat.AttackPower => "Your basic attack — the plain swing between skills.",
        HunterStat.ResonanceAffinity => "Your skills' damage.",
        HunterStat.CriticalChance => "How often a hit becomes a critical hit.",
        HunterStat.Focus => "How hard a critical hit lands.",
        HunterStat.MaxHealth => "The base of your hunter's life.",
        HunterStat.Defense => "How much smaller every hit you take becomes.",
        HunterStat.Vitality => "Life you regain every second of a fight.",
        HunterStat.Engineering => "How fast you act — swings, skills and animations.",
        _ => "How much a cleared wave pays.",
    };
}
