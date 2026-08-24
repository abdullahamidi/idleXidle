using System;

namespace ResonanceHunter.Core.Expeditions;

/// <summary>
/// Every knob for the wave loop: how threat and reward scale, and how often a boss lands.
/// </summary>
/// <remarks>
/// These were the shared half of the retired squad engine's tuning. The squad-only knobs (exit shares,
/// role mitigation, focus-fire) left with the <c>Expedition</c> class; what remains is what the live
/// single-champion loop (<see cref="ResonanceHunter.Core.Builds.SoloExpedition"/>) and its per-wave sim
/// still read.
/// </remarks>
public sealed record ExpeditionTuning
{
    /// <summary>Threat COMPOUNDS per wave...</summary>
    /// <summary>How enemy HEALTH compounds per wave.</summary>
    /// <remarks>
    /// MEASURED AND LEFT ALONE. Making health outgrow damage looks like the obvious answer to ENDURE
    /// dominating the depth metric, and it is not: swept across six pairs from 1.06/1.06 to 1.150/1.030,
    /// the best-to-worst branch ratio never once improved on the 1.52 it starts at, and the first step
    /// away made it WORSE (1.72). Longer fights feed a sustain branch — it out-heals the extra hits and
    /// banks the extra time — while the damage branches simply eat more bites per wave.
    ///
    /// The separate damage base stays because health and damage compounding at one rate was an
    /// unexamined coupling, and having the two knobs is worth more than the one line it costs.
    /// </remarks>
    public float EnemyScaleBase { get; init; } = 1.06f;

    /// <summary>
    /// How enemy DAMAGE compounds per wave. Deliberately below <see cref="EnemyScaleBase"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE ONE NUMBER THAT MADE THE MASTERY TREE FOUR CHOICES. Health and damage used to compound at the
    /// same 1.06, and the consequence was structural rather than numeric: nothing ever stalled, so the
    /// only thing that ended a run was the champion dying, so depth measured survival and nothing else.
    /// ENDURE won all six regions. Tightening the stall ceiling from 120s to 25s changed not one wave,
    /// because a run never reached the ceiling.
    /// </para>
    /// <para>
    /// Two wrong answers were measured first, and both are worth recording. Raising HEALTH growth makes
    /// it WORSE (1.52 -> 1.72): longer fights feed a sustain branch, which out-heals the extra bites and
    /// banks the extra time, while a damage branch just eats more of them. And no single ENDURE field is
    /// the culprit — knocking out leech, absorb, regen or the health multiplier one at a time costs
    /// between zero and seven waves, because ENDURE is not dying at all. It is STALL-bound at ~50 while
    /// the other three die at 29-41.
    /// </para>
    /// <para>
    /// So the fix is to let the damage branches live long enough to reach their own stall walls, which
    /// sit further out than ENDURE's precisely because they kill faster. Isolating this one knob with
    /// health held at 1.06:
    /// </para>
    /// <code>
    ///   dmg    Weight  Spread  Tempo  Endure   ratio
    ///   1.060      31      38     41      47    1.52
    ///   1.050      35      40     42      51    1.46
    ///   1.040      39      41     43      52    1.33   &lt;- here
    ///   1.030      41      43     45      60    1.46
    /// </code>
    /// <para>
    /// Below 1.04 it turns back: everyone survives so long that ENDURE's stall wall is the only limit
    /// left and it runs away again. 1.04 is the floor of that curve, not a round number chosen for
    /// looking tidy.
    /// </para>
    /// <para>
    /// EARLY PACING IS UNAFFECTED, which was checked rather than assumed — skill points are
    /// <c>BestDepth / 5</c> per region, so anything that moves depth moves the income curve with it.
    /// Median depth for an untouched build across forty runs, at 1.06 against 1.04:
    /// </para>
    /// <code>
    ///   ranks   before  after   points/region
    ///       0        5      5     1 -> 1
    ///       2        5      5     1 -> 1
    ///       5        7      8     1 -> 1
    ///      10       10     10     2 -> 2
    ///      20       13     14     2 -> 2
    /// </code>
    /// <para>
    /// Identical income at every training level. The two rates barely differ before wave 15, so the
    /// change reshapes the deep game only — which is where the imbalance was.
    /// </para>
    /// </remarks>
    public float EnemyDamageScaleBase { get; init; } = 1.04f;

    /// <summary>...while reward only grows LINEARLY. The gap is the whole design.</summary>
    public float HaulScaleSlope { get; init; } = 0.35f;

    /// <summary>Every Nth wave is a boss — a spike you can see coming. See <see cref="WaveScaling.IsBossWave"/>.</summary>
    public int BossEvery { get; init; } = 5;

    /// <summary>A boss's health on top of the wave's own scaling. Its haul is scaled to match.</summary>
    public float BossHealthScale { get; init; } = 2.2f;

    /// <summary>Hard stop so a fight that cannot be won can never hang the sim.</summary>
    public int TickCeilingMs { get; init; } = 120_000;
    public int TickMs { get; init; } = 100;

    /// <summary>
    /// The flat pause every active skill takes at a wave's start (unless PREPARATION waives openings).
    /// </summary>
    /// <remarks>
    /// Flat, not cooldown-proportional — a proportional breath taxed heavy builds hardest and broke
    /// the branch balance sweep. See the note at the top of SoloBattle.ResolveWave's skill setup.
    /// </remarks>
    public int WaveOpeningMs { get; init; } = 700;

    public static ExpeditionTuning Default { get; } = new();
}

/// <summary>How a wave ended.</summary>
public enum WaveOutcome { Cleared, Wiped, Stalled }

/// <summary>
/// A beat of the auto-fight the presentation can react to. Pure data (no MonoGame), so the sim can be
/// unit-tested without a window and the view replays these to animate it.
/// </summary>
/// <remarks>
/// <see cref="BattleEventKind.Shield"/> carries the shield's remaining duration as its Amount, and its Slot
/// says WHO is covered — the sim decides coverage, the screen only draws it, so the rule lives in one place.
/// </remarks>
// Charge: Value is the pool AFTER the change — the HUD latches the latest one at its playhead
// rather than re-deriving the sim's rules (a replayed rule is a rule that can drift).
public enum BattleEventKind { Strike, EnemyStrike, Down, Heal, EnemyDown, Skill, Shield, Charge }

/// <summary>A single beat of the fight, for the presentation layer to replay.</summary>
/// <param name="Slot">
/// Who the beat is about. For <see cref="BattleEventKind.EnemyStrike"/> and
/// <see cref="BattleEventKind.Heal"/> this is the champion's slot; for
/// <see cref="BattleEventKind.Strike"/> and <see cref="BattleEventKind.EnemyDown"/> it is the INDEX OF
/// THE CREATURE in the wave's composition. For <see cref="BattleEventKind.Skill"/> it is the casting
/// skill's <c>(int)Source</c> (2026-08-22: the HUNT screen tints the Form's effect by it) and
/// <c>Amount</c> is the <c>(int)Form</c>.
/// </param>
/// <remarks>
/// A wave used to hold one enemy, so every Strike carried slot 0 and the replay could keep a single
/// aggregate health bar. Now that a wave is a composition, the index is what lets the screen show five
/// creatures dying one at a time instead of one bar draining — which is the difference between a player
/// being able to see "I killed two of five" and not.
/// </remarks>
public readonly record struct BattleEvent(BattleEventKind Kind, int Slot, int Amount, int AtMs);

/// <summary>Skill payouts earned mid-wave (SALVAGE quality, HARVEST/LODESTONE cores), collected at wave end.</summary>
public sealed class WaveBonus
{
    public float Quality { get; private set; }

    /// <summary>
    /// Spare cores paid by the HARVEST enchant and the LODESTONE keystone.
    /// </summary>
    /// <remarks>
    /// THE CORES CURRENCY WAS RETIRED 2026-08-24 with the creature subsystem, so nothing in the game
    /// consumes this channel any more — the host stopped reading <see cref="Haul.Cores"/>. The channel
    /// is kept (rather than deleted) because two live build pieces still pay into it and re-pointing
    /// their reward is real again since 2026-08-24: the host pays it out as the forge material CORE
    /// (the re-roll currency), which is the word both effects already used. This is also the
    /// measurable proof their triggers still fire (TriggerLivenessTests reads it).
    /// </remarks>
    public int Cores { get; private set; }

    public void AddQuality(float q) => Quality += q;
    public void AddCores(int c) => Cores += c;
}

/// <summary>What a wave earned: gleam, the quality that tilts its loot — and the skill-paid cores.</summary>
public readonly record struct Haul(int Cores, int Gleam, float Quality)
{
    // NOTE on Cores: the hatchery currency this fed retired 2026-08-24. Only the HARVEST/LODESTONE
    // skill payouts write it now, and the host pays it out as the forge material CORE (see
    // WaveBonus.Cores) — so the two effects' own words ("core") stay true.

    /// <summary>Fold in another wave's haul; quality is the best of the two, not the sum.</summary>
    public Haul Plus(Haul o) => new(Cores + o.Cores, Gleam + o.Gleam, MathF.Max(Quality, o.Quality));
}

/// <summary>
/// How a wave's threat and reward scale with depth, and where the boss spikes land.
/// </summary>
/// <remarks>
/// This is all that survived the squad auto-battler: the fight itself is now
/// <see cref="ResonanceHunter.Core.Builds.SoloBattle"/>, but the SHAPE of the run — threat compounding
/// faster than reward, a boss every fifth wave — is shared, deterministic, and lives here. Everything is a
/// pure function of the wave number and the tuning, so it is trivially testable and has no state.
/// </remarks>
public static class WaveScaling
{
    /// <summary>Threat compounds; see <see cref="ExpeditionTuning.EnemyScaleBase"/>. Includes the boss
    /// spike, so this is the HEALTH scale — pair it with <see cref="EnemyDamageScale"/>.</summary>
    public static float EnemyScale(int wave, ExpeditionTuning t)
        => MathF.Pow(t.EnemyScaleBase, wave) * (IsBossWave(wave, t) ? t.BossHealthScale : 1f);

    /// <summary>
    /// The same compounding threat WITHOUT the boss spike — the scale enemy DAMAGE uses.
    /// </summary>
    /// <remarks>
    /// <see cref="EnemyScale"/> folds in <see cref="ExpeditionTuning.BossHealthScale"/>, and callers were
    /// passing it to both the health and the damage parameter. A boss therefore also hit 2.2x harder than
    /// the wave before it — a spike this type's own documentation never claimed, and which no tuning knob
    /// could remove without also removing the health spike it is named for.
    ///
    /// MEASURED, so the claim stays honest: over a health sweep from 200 to 2000 the bug costs roughly
    /// two to four waves of depth. It did NOT visibly quantise depth to boss waves on that axis — two of
    /// twelve distinct depths landed on a multiple of five with the bug, two of fifteen without it — so
    /// the case for the fix is the contradiction with the design, not a dramatic shape change.
    /// </remarks>
    public static float EnemyDamageScale(int wave, ExpeditionTuning t)
        => MathF.Pow(t.EnemyDamageScaleBase, wave);

    /// <summary>
    /// Every fifth wave is a boss. The run needs a heartbeat, not a gradient.
    /// </summary>
    /// <remarks>
    /// Threat compounding smoothly is a ramp with no landmarks, so every wave reads the same. A spike you
    /// can SEE coming turns "push or hold" into a real question — and gives VOW OF THE BOUND ("only against
    /// bosses") something to be true about.
    /// </remarks>
    public static bool IsBossWave(int wave, ExpeditionTuning t) => wave > 0 && wave % t.BossEvery == 0;

    /// <summary>
    /// Reward is linear — so it falls behind threat BY DESIGN. A boss pays for the spike it is.
    /// </summary>
    /// <remarks>
    /// The boss bonus deliberately matches its health scale. A boss that were merely tougher would be a tax
    /// on pushing, and the answer to "push into the boss?" would always be no — which is a wall, not a bet.
    /// </remarks>
    public static float HaulScale(int wave, ExpeditionTuning t)
        => (1f + t.HaulScaleSlope * wave) * (IsBossWave(wave, t) ? t.BossHealthScale : 1f);
}
