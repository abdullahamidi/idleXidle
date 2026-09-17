using System;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Sources;

namespace IdleXIdle.Core.Traits;

/// <summary>
/// The dials the twenty-six TRAITS turn — one typed group hanging off <see cref="Builds.SkillShape"/>
/// rather than thirty more fields on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a sub-record.</b> <c>SkillShape</c> is already about ninety fields, and
/// <c>SkillCatalogue.cs</c> records the same decision for <c>SkillRules</c>: a record past the width
/// where a reader stops reading gets a nested group instead of another column. The cost is one hop at
/// each read site (<c>shape.Traits.X</c>) and a second <see cref="Combine"/> to keep in step; the
/// benefit is that a build with no trait equipped composes byte-identically to one from before traits
/// existed, because every dial here is neutral at its default.
/// </para>
/// <para>
/// <b>Every field is read in exactly one place in the fight.</b> That is not a nicety — a dial nothing
/// reads is this project's named failure mode, and the trait liveness suite
/// (<c>trait_liveness_test.cs</c>) exists to prove there is not one: for each trait it runs the same
/// fight with and without, and the observable difference must move in the stated direction when the
/// trait's condition is posed.
/// </para>
/// <para>
/// <b>Not a scripting layer.</b> No dispatch, no ordering, nothing to register. Each member is a plain
/// value with a single consumer, exactly like <c>SkillRules</c>.
/// </para>
/// </remarks>
/// <param name="HeavyHitArmourStrip">DEEP CUT — armour stripped from a creature by a hit worth <see cref="Builds.SoloBattle.HeavyHitFraction"/> or more of its maximum health. The THRESHOLD is a constant of the fight, not a dial: the wave counts heavy hits whether or not the trait is worn, which is what lets the trait be discovered before it is owned.</param>
/// <param name="OverkillToShield">THE SPILL — the share of damage wasted past a kill that comes back as shield.</param>
/// <param name="OverkillToBleed">CARRION WEIGHT — the share of that same waste that bleeds into the rest of the wave.</param>
/// <param name="WideCastAtTargets">SPREADING FIRE — the reach at which a cast is already "wide" and earns the extra creature.</param>
/// <param name="WideCastExtraTargets">SPREADING FIRE — how many more creatures a wide cast reaches.</param>
/// <param name="FirstHitOfWaveCritMultiplier">THE CERTAIN HAND — the first skill hit of each wave crits outright, this many times as hard as a critical usually is.</param>
/// <param name="CritToBleed">THE OPENED VEIN — the share of a hit that lingers as bleed, paid at the rate the hit criticals.</param>
/// <param name="ShieldAfterBreakBonus">SCAR TISSUE — how much larger the first shield granted after a break is.</param>
/// <param name="ShieldCarryFraction">STANDING PLATE — the share of a shield still held at a wave's end that is carried into the next. WHOLE, against the half everybody carries (<c>ShieldRules.BaseCarryFraction</c>).</param>
/// <param name="ShieldedReflectFraction">THE ANSWERING WALL — the share of its own bite a creature takes back while the champion is holding a shield.</param>
/// <param name="LowHealthShare">LAST BREATH — the health share at or under which the champion acts faster.</param>
/// <param name="LowHealthRateBonus">LAST BREATH — and how much faster.</param>
/// <param name="LethalBiteShare">THE THIN LINE — what the first bite that would kill the champion each wave deals instead.</param>
/// <param name="HealCeilingPerHeal">PRACTISED FLESH — how much wider each heal makes this wave's healing ceiling.</param>
/// <param name="HealCeilingPerHealCap">PRACTISED FLESH — and how far that widening may go.</param>
/// <param name="OverhealToDamage">THE GIVEN HAND — the share of healing the pool had no room for that is thrown at the enemy instead.</param>
/// <param name="ReflectRampPerBite">THE MIRROR — how much stronger each reflection is for every bite already taken this wave.</param>
/// <param name="ReflectRampCap">THE MIRROR — and its ceiling.</param>
/// <param name="PowerPerDeadEnemy">SETTLING WEIGHT — how much heavier every hit lands for each creature the wave has already lost.</param>
/// <param name="PowerPerDeadCap">SETTLING WEIGHT — and its ceiling.</param>
/// <param name="LastEnemyBonus">LAST WORD — how much harder the last living creature in a wave is hit.</param>
/// <param name="AmplifyWindowPerEnemyMs">THE LINGERING MARK — how much longer a cast amplify window holds for each creature still alive when it opens.</param>
/// <param name="UnswornBuildBorrowsWeakestVow">THE KEPT WORD — a build that has sworn no vow at all is held by the weakest vow the account has found, and paid by it.</param>
/// <param name="VowPayPerExtraKeptVow">THE WEIGHT OF VOWS — how much more every vow pays for each vow past the first whose rule the build is keeping.</param>
/// <param name="OneSourceRepeatsFirstCast">THE SINGLE NOTE — with every woven skill on one source, the wave's first cast lands twice.</param>
/// <param name="AllMatchupsStrongAtSources">MANY TONGUES — the number of distinct sources at which every matchup counts as strong.</param>
/// <param name="MatchedSuitSource">THE MATCHED SUIT — the element of the five-piece set being worn, resolved at composition time.</param>
/// <param name="MatchedSuitBonus">THE MATCHED SUIT — how much harder a skill of that element lands.</param>
/// <param name="WeakenPerDeadEnemy">THE STUDIED PLACE — how much softer the wave bites for each creature it has lost, one step per region mastered.</param>
/// <param name="WeakenPerDeadCap">THE STUDIED PLACE — the floor that weakening cannot pass.</param>
/// <param name="GrudgeArchetype">WHAT KILLED YOU — the kind of creature that ended the last three descents, resolved at composition time.</param>
/// <param name="GrudgeBonus">WHAT KILLED YOU — how much more that kind takes.</param>
/// <param name="PreventFirstBiteAtFullHealth">THE UNBROKEN THREAD — at full health, the first bite of each wave is stopped outright.</param>
public sealed record TraitRules(
    float HeavyHitArmourStrip = 0f,
    float OverkillToShield = 0f,
    float OverkillToBleed = 0f,
    int WideCastAtTargets = 0,
    int WideCastExtraTargets = 0,
    float FirstHitOfWaveCritMultiplier = 0f,
    float CritToBleed = 0f,
    float ShieldAfterBreakBonus = 0f,
    float ShieldCarryFraction = 0f,
    float ShieldedReflectFraction = 0f,
    float LowHealthShare = 0f,
    float LowHealthRateBonus = 0f,
    float LethalBiteShare = 0f,
    float HealCeilingPerHeal = 0f,
    float HealCeilingPerHealCap = 0f,
    float OverhealToDamage = 0f,
    float ReflectRampPerBite = 0f,
    float ReflectRampCap = 0f,
    float PowerPerDeadEnemy = 0f,
    float PowerPerDeadCap = 0f,
    float LastEnemyBonus = 0f,
    int AmplifyWindowPerEnemyMs = 0,
    bool UnswornBuildBorrowsWeakestVow = false,
    float VowPayPerExtraKeptVow = 0f,
    bool OneSourceRepeatsFirstCast = false,
    int AllMatchupsStrongAtSources = 0,
    Source? MatchedSuitSource = null,
    float MatchedSuitBonus = 0f,
    float WeakenPerDeadEnemy = 0f,
    float WeakenPerDeadCap = 0f,
    Archetype? GrudgeArchetype = null,
    float GrudgeBonus = 0f,
    bool PreventFirstBiteAtFullHealth = false)
{
    /// <summary>No trait equipped. Every dial neutral, so the fight behaves exactly as it did before.</summary>
    public static readonly TraitRules None = new();

    /// <summary>
    /// Fold two sets of trait dials together. Additives add, flags OR, thresholds take the kinder
    /// value, and a resolved identity (an element, an archetype) takes whichever half named one.
    /// </summary>
    /// <remarks>
    /// The same rule <c>SkillShape.Combine</c> documents, for the same reason: two traits that each
    /// widen a condition must not stack the CONDITION, only the payment. Only three slots are worn at
    /// once and no two traits in the catalogue share a dial, so in practice this is a merge rather
    /// than an arithmetic — but the fold has to be right anyway, because a later trait added to the
    /// catalogue would find it.
    /// </remarks>
    public static TraitRules Combine(TraitRules a, TraitRules b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (ReferenceEquals(a, None)) return b;
        if (ReferenceEquals(b, None)) return a;

        return new TraitRules(
            HeavyHitArmourStrip: a.HeavyHitArmourStrip + b.HeavyHitArmourStrip,
            OverkillToShield: a.OverkillToShield + b.OverkillToShield,
            OverkillToBleed: a.OverkillToBleed + b.OverkillToBleed,
            WideCastAtTargets: Kinder(a.WideCastAtTargets, b.WideCastAtTargets),
            WideCastExtraTargets: a.WideCastExtraTargets + b.WideCastExtraTargets,
            FirstHitOfWaveCritMultiplier: MathF.Max(a.FirstHitOfWaveCritMultiplier, b.FirstHitOfWaveCritMultiplier),
            CritToBleed: a.CritToBleed + b.CritToBleed,
            ShieldAfterBreakBonus: a.ShieldAfterBreakBonus + b.ShieldAfterBreakBonus,
            ShieldCarryFraction: MathF.Max(a.ShieldCarryFraction, b.ShieldCarryFraction),
            ShieldedReflectFraction: a.ShieldedReflectFraction + b.ShieldedReflectFraction,
            LowHealthShare: MathF.Max(a.LowHealthShare, b.LowHealthShare),
            LowHealthRateBonus: a.LowHealthRateBonus + b.LowHealthRateBonus,
            LethalBiteShare: Softer(a.LethalBiteShare, b.LethalBiteShare),
            HealCeilingPerHeal: a.HealCeilingPerHeal + b.HealCeilingPerHeal,
            HealCeilingPerHealCap: MathF.Max(a.HealCeilingPerHealCap, b.HealCeilingPerHealCap),
            OverhealToDamage: a.OverhealToDamage + b.OverhealToDamage,
            ReflectRampPerBite: a.ReflectRampPerBite + b.ReflectRampPerBite,
            ReflectRampCap: MathF.Max(a.ReflectRampCap, b.ReflectRampCap),
            PowerPerDeadEnemy: a.PowerPerDeadEnemy + b.PowerPerDeadEnemy,
            PowerPerDeadCap: MathF.Max(a.PowerPerDeadCap, b.PowerPerDeadCap),
            LastEnemyBonus: a.LastEnemyBonus + b.LastEnemyBonus,
            AmplifyWindowPerEnemyMs: a.AmplifyWindowPerEnemyMs + b.AmplifyWindowPerEnemyMs,
            UnswornBuildBorrowsWeakestVow: a.UnswornBuildBorrowsWeakestVow || b.UnswornBuildBorrowsWeakestVow,
            VowPayPerExtraKeptVow: a.VowPayPerExtraKeptVow + b.VowPayPerExtraKeptVow,
            OneSourceRepeatsFirstCast: a.OneSourceRepeatsFirstCast || b.OneSourceRepeatsFirstCast,
            AllMatchupsStrongAtSources: Kinder(a.AllMatchupsStrongAtSources, b.AllMatchupsStrongAtSources),
            MatchedSuitSource: a.MatchedSuitSource ?? b.MatchedSuitSource,
            MatchedSuitBonus: a.MatchedSuitBonus + b.MatchedSuitBonus,
            WeakenPerDeadEnemy: MathF.Max(a.WeakenPerDeadEnemy, b.WeakenPerDeadEnemy),
            WeakenPerDeadCap: MathF.Max(a.WeakenPerDeadCap, b.WeakenPerDeadCap),
            GrudgeArchetype: a.GrudgeArchetype ?? b.GrudgeArchetype,
            GrudgeBonus: a.GrudgeBonus + b.GrudgeBonus,
            PreventFirstBiteAtFullHealth: a.PreventFirstBiteAtFullHealth || b.PreventFirstBiteAtFullHealth);
    }

    /// <summary>A count threshold: unset loses, and the LOWER of two set values is the kinder one.</summary>
    private static int Kinder(int a, int b)
    {
        if (a <= 0) return b;
        if (b <= 0) return a;
        return Math.Min(a, b);
    }

    /// <summary>A share of a blow the champion still takes: unset loses, and the SMALLER share is kinder.</summary>
    private static float Softer(float a, float b)
    {
        if (a <= 0f) return b;
        if (b <= 0f) return a;
        return MathF.Min(a, b);
    }
}
