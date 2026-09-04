using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Encounters;
using IdleXIdle.Core.Expeditions;

namespace IdleXIdle.Core.Traits;

/// <summary>
/// What ONE cleared wave contributes to the account's accumulators.
/// </summary>
/// <remarks>
/// <para>
/// Built in <see cref="Builds.SoloExpedition"/> from the wave's own <see cref="Builds.WaveMetrics"/>
/// plus the build facts the metrics do not carry. It is a value, not a reference to the fight, so a
/// wave that has ended cannot be re-read or edited by anything downstream.
/// </para>
/// <para>
/// <b>Only a CLEARED wave feeds this.</b> That matches the rule the game already has for skill
/// experience — a run that dies teaches nothing on its way out. WHAT KILLED YOU is the deliberate
/// exception and is evaluated at run end, from the saved run log, which is how §36's "failure"
/// behaviour is covered.
/// </para>
/// </remarks>
public readonly record struct TraitWaveFacts(
    int ChampionMaxHealth,
    int HeavyHits,
    float Overkill,
    float ShieldGained,
    int ShieldBreaks,
    float ShieldAbsorbed,
    int Healed,
    float ReflectedDamage,
    float LowestHealthFraction,
    int HealthLost,
    int CreaturesKilled,
    int SignCasts,
    bool CarriedWideSkill,
    float CritPercent,
    bool OneElement,
    int DistinctElements,
    bool VowSwornAndBroken);

/// <summary>
/// The six facts the ACCOUNT already keeps, which six traits read directly.
/// </summary>
/// <remarks>
/// This is the migration gift §96 allows: those six can be discovered retroactively on the first load
/// of a build that has traits, because their counters already exist in every established save.
/// Everything else starts at zero and is earned honestly — which is exactly "do not automatically
/// unlock all new Traits because the old tree was progressed".
/// </remarks>
public readonly record struct TraitAccount(
    int BossesFelled = 0,
    int RunsWithVowKept = 0,
    int SetsCompleted = 0,
    int RegionsConquered = 0,
    int RegionsMastered = 0,
    int WallStreak = 0)
{
    /// <summary>A fresh account — a probe, or a brand new game.</summary>
    public static TraitAccount None => new();
}

/// <summary>
/// The ONE thing that awakens a trait. Hidden from the player, deterministic in the engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hidden does not mean random</b> (§28-§31, LAW 8). Every rule in <see cref="TraitCatalogue"/> is
/// a counter and a number; nothing here reads an <see cref="Random"/>, waits for a proc, or needs a
/// drop. Creature COUNT and ARCHETYPE are rolled by the band, so no rule reads either: each one reads
/// the player's own build, an account-monotone total that the roll only PACES, or a deterministic
/// fact — a boss, a conquest, a completed set, a sworn vow.
/// </para>
/// <para>
/// <b>Nothing can be missed.</b> Every counter is monotone and every rule is re-checked on every
/// pass — after a cleared wave, at the end of a run, and once on load. A player who was not watching
/// when a threshold was crossed still owns the trait the next time anything is evaluated.
/// </para>
/// <para>
/// <b>This is the only writer of the discovered set.</b> Nothing else in the game may call
/// <see cref="TraitLedger.Discover"/>.
/// </para>
/// </remarks>
public static class TraitDiscovery
{
    /// <summary>
    /// The critical chance, in percent, at which a wave counts as a CRITICAL wave.
    /// </summary>
    /// <remarks>
    /// A build fact, read once per wave off <c>BuildContext.CritPercent</c>, so it is a decision the
    /// player made at the workbench rather than something the wave rolled.
    /// </remarks>
    public const float HighCritPercent = 35f;

    /// <summary>The health share at or below which a wave counts as fought at the brink.</summary>
    public const float BrinkShare = 0.20f;

    /// <summary>The reach at which a woven skill makes the wave a WIDE one.</summary>
    public const int WideTargets = 3;

    /// <summary>The number of distinct elements at which a wave counts as a motley one.</summary>
    public const int MotleyElements = 4;

    /// <summary>
    /// Fold one cleared wave into the accumulators, then re-check every rule.
    /// </summary>
    /// <returns>The ids that awakened on this call, in catalogue order. Empty is the normal answer.</returns>
    public static IReadOnlyList<string> OnWaveCleared(
        TraitLedger ledger, TraitWaveFacts wave, TraitAccount account, TraitFirst first)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        // The five POOL-denominated counters divide by the pool AT THE TIME, so a hunter whose pool
        // grows tenfold over a career earns them at a constant rate. That is the intent: the fantasy
        // is proportional survival, not an absolute figure that stops meaning anything at depth.
        var pool = (double)Math.Max(1, wave.ChampionMaxHealth);

        ledger.Add(TraitCounter.HeavyHits, wave.HeavyHits);
        ledger.Add(TraitCounter.OverkillPools, wave.Overkill / pool);
        ledger.Add(TraitCounter.ShieldGainedPools, wave.ShieldGained / pool);
        ledger.Add(TraitCounter.ShieldBreaks, wave.ShieldBreaks);
        ledger.Add(TraitCounter.ShieldAbsorbedPools, wave.ShieldAbsorbed / pool);
        ledger.Add(TraitCounter.HealedPools, wave.Healed / pool);
        ledger.Add(TraitCounter.ReflectedPools, wave.ReflectedDamage / pool);
        ledger.Add(TraitCounter.CreaturesKilled, wave.CreaturesKilled);
        ledger.Add(TraitCounter.SignCasts, wave.SignCasts);

        if (wave.LowestHealthFraction <= BrinkShare) ledger.Add(TraitCounter.BrinkWaves, 1);
        if (wave.HealthLost <= 0) ledger.Add(TraitCounter.UntouchedWaves, 1);
        if (wave.CarriedWideSkill) ledger.Add(TraitCounter.WideWaves, 1);
        if (wave.CritPercent >= HighCritPercent) ledger.Add(TraitCounter.HighCritWaves, 1);
        if (wave.OneElement) ledger.Add(TraitCounter.PureWaves, 1);
        if (wave.DistinctElements >= MotleyElements) ledger.Add(TraitCounter.MotleyWaves, 1);
        // THE ONE COUNTER A WAVE FEEDS WHETHER IT IS CLEARED OR NOT would be this one — but it is fed
        // here like the rest, because a build that swore a promise it cannot keep goes on being that
        // build. Fought, not survived, is the sentence the trait is for.
        if (wave.VowSwornAndBroken) ledger.Add(TraitCounter.BrokenVowWaves, 1);

        return Evaluate(ledger, account, first);
    }

    /// <summary>
    /// Re-check every rule without adding anything: at the end of a run, and once on load.
    /// </summary>
    /// <remarks>
    /// The load pass is what makes an established save's six account-fed traits awaken at once — and
    /// §32 warns against a ceremony every few minutes, so the host announces those as ONE combined
    /// plate rather than six in a second. Every pass after that reveals one at a time.
    /// </remarks>
    public static IReadOnlyList<string> Evaluate(TraitLedger ledger, TraitAccount account, TraitFirst first)
    {
        ArgumentNullException.ThrowIfNull(ledger);

        List<string>? awakened = null;
        foreach (var def in TraitCatalogue.All)
        {
            if (ledger.Has(def.Id)) continue;
            if (Value(ledger, account, def.Discovery.Counter) < def.Discovery.Threshold) continue;
            if (!ledger.Discover(def.Id, first)) continue;
            (awakened ??= new List<string>()).Add(def.Id);
        }
        return (IReadOnlyList<string>?)awakened ?? Array.Empty<string>();
    }

    /// <summary>
    /// What a counter stands at: the ledger's own accumulators, or the account's live figures.
    /// </summary>
    /// <remarks>
    /// The account facts are NOT copied into the ledger. They already have one home apiece in the
    /// save, and a second copy is a second truth — the failure this project keeps paying for. They
    /// are read where they live, every time a rule is checked.
    /// </remarks>
    private static double Value(TraitLedger ledger, TraitAccount account, TraitCounter counter) => counter switch
    {
        TraitCounter.BossesFelled => account.BossesFelled,
        TraitCounter.RunsWithVowKept => account.RunsWithVowKept,
        TraitCounter.SetsCompleted => account.SetsCompleted,
        TraitCounter.RegionsConquered => account.RegionsConquered,
        TraitCounter.RegionsMastered => account.RegionsMastered,
        TraitCounter.WallStreak => account.WallStreak,
        _ => ledger.Of(counter),
    };

    /// <summary>
    /// How many of the most recent descents ended in ONE region against ONE kind of creature.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT KILLED YOU's rule, derived fresh from the saved run log rather than accumulated — which is
    /// why it is the one counter that is not monotone, and why it is safe: the log is ten entries and
    /// already persisted, so nothing new is stored (§29).
    /// </para>
    /// <para>
    /// <b>One region.</b> Across regions the sentence reads oddly — angry at the armoured of one place
    /// because the armoured of another killed you. Slower to earn and coherent.
    /// </para>
    /// </remarks>
    /// <param name="newestFirst">The run log, newest first — the order <see cref="RunLog.Entries"/> keeps.</param>
    public static (int Streak, Archetype? Wall) WallStreakOf(IEnumerable<RunReport> newestFirst)
    {
        if (newestFirst is null) return (0, null);

        string? region = null;
        Archetype? wall = null;
        var streak = 0;
        foreach (var run in newestFirst)
        {
            // A cleared or stalled run is not a wall. Only a death names one.
            if (run.Outcome != WaveOutcome.Wiped) break;
            if (streak == 0) { region = run.RegionId; wall = run.WallArchetype; }
            else if (run.RegionId != region || run.WallArchetype != wall) break;
            streak++;
        }
        return (streak, wall);
    }
}
