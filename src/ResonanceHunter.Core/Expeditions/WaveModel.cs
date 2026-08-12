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
    public float EnemyScaleBase { get; init; } = 1.06f;

    /// <summary>...while reward only grows LINEARLY. The gap is the whole design.</summary>
    public float HaulScaleSlope { get; init; } = 0.35f;

    /// <summary>Every Nth wave is a boss — a spike you can see coming. See <see cref="WaveScaling.IsBossWave"/>.</summary>
    public int BossEvery { get; init; } = 5;

    /// <summary>A boss's health on top of the wave's own scaling. Its haul is scaled to match.</summary>
    public float BossHealthScale { get; init; } = 2.2f;

    /// <summary>Hard stop so a fight that cannot be won can never hang the sim.</summary>
    public int TickCeilingMs { get; init; } = 120_000;
    public int TickMs { get; init; } = 100;

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
public enum BattleEventKind { Strike, EnemyStrike, Down, Heal, EnemyDown, Skill, Shield }

/// <summary>A single beat of the fight, for the presentation layer to replay.</summary>
/// <param name="Slot">
/// Who the beat is about. For <see cref="BattleEventKind.EnemyStrike"/> and
/// <see cref="BattleEventKind.Heal"/> this is the champion's slot; for
/// <see cref="BattleEventKind.Strike"/> and <see cref="BattleEventKind.EnemyDown"/> it is the INDEX OF
/// THE CREATURE in the wave's composition.
/// </param>
/// <remarks>
/// A wave used to hold one enemy, so every Strike carried slot 0 and the replay could keep a single
/// aggregate health bar. Now that a wave is a composition, the index is what lets the screen show five
/// creatures dying one at a time instead of one bar draining — which is the difference between a player
/// being able to see "I killed two of five" and not.
/// </remarks>
public readonly record struct BattleEvent(BattleEventKind Kind, int Slot, int Amount, int AtMs);

/// <summary>Skill payouts earned mid-wave (SALVAGE quality, BLOOM/HARVEST cores), collected at wave end.</summary>
public sealed class WaveBonus
{
    public float Quality { get; private set; }
    public int Cores { get; private set; }
    public void AddQuality(float q) => Quality += q;
    public void AddCores(int c) => Cores += c;
}

/// <summary>What a wave earned: cores, gleam, and the quality that tilts its loot.</summary>
public readonly record struct Haul(int Cores, int Gleam, float Quality)
{
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
        => MathF.Pow(t.EnemyScaleBase, wave);

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
