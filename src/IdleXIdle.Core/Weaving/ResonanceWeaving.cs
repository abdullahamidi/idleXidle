using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Automation;
using IdleXIdle.Core.Builds;
using IdleXIdle.Core.Sources;

namespace IdleXIdle.Core.Abilities;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// THE LEGACY RUMP. This file held the SOURCE × FORM weaving library; the refactor is draining it.
// Vows moved to Builds/Vows.cs and the Source matchup to Sources/SourceMatchup.cs (2026-08-31, P3a).
// What remains — Form, WovenAbility, the Form base-damage table — dies with P3c, when SkillDef owns
// its own BasePower and the fight stops asking Form for anything.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>What an ability DOES. Form is the verb.</summary>
public enum Form { Strike, Projectile, Aura, Trap, Mark, Transformation }

/// <summary>The rump of the old weaving tuning: the Form damage table and the resonance coefficient.</summary>
/// <remarks>
/// The Vow half became <see cref="Builds.VowTuning"/>; the matchup half became
/// <see cref="Sources.SourceMatchup"/> (the two multiplier properties below are shims onto it, so a
/// screen quoting the matchup still reads the one live number). Deleted with P3c.
/// </remarks>
public sealed record WeavingTuning
{
    /// <summary>How strongly the Hunter's resonance_affinity feeds ability power.</summary>
    /// <remarks>
    /// 0.018, from 0.008 (2026-08-26). Until then a skill hit was scaled by BOTH stats — RESONANCE here
    /// and MIGHT through the shared damage multiplier (1 + 0.010 × MIGHT). MIGHT is the basic attack's
    /// alone now, so RESONANCE carries what two stats carried: (1 + 0.010x)(1 + 0.008x) ≈ 1 + 0.018x for
    /// the stat totals a trained hunter has. Measured on the mastery sweep: with 0.008 the damage
    /// branches lost ~30% depth against ENDURE when MIGHT left; 0.018 puts them back.
    /// </remarks>
    public float SourceScalingCoefficient { get; init; } = 0.018f;

    /// <summary>Shim onto <see cref="Sources.SourceMatchup.Strong"/> — the one live number.</summary>
    public float StrongMultiplier => SourceMatchup.Strong;

    /// <summary>Shim onto <see cref="Sources.SourceMatchup.Weak"/>.</summary>
    public float WeakMultiplier => SourceMatchup.Weak;

    /// <summary>
    /// What one activation of each Form is worth. These are HIT SIZES, and the spread between them is
    /// load-bearing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They used to sit between 22 and 55 — a 2.5x spread, and only 1.18x between Strike and Projectile.
    /// Enemy armour is flat per hit (<see cref="Builds.SoloBattle.MinHitFraction"/>), so it reads hit
    /// SIZE; with the old numbers it cost a small-hit build 58% and a large-hit build 52%, which is the
    /// same thing twice. The Armoured archetype and the whole Weight axis of the skill tree had nothing
    /// to be about, because there was no such thing as a large hit.
    /// </para>
    /// <para>
    /// Cooldowns are deliberately UNCHANGED. They are tuned against wave length, and this file's own
    /// history records what happens when that is forgotten: 5-10s cooldowns meant skills that never
    /// fired at all. Spreading damage alone is the change that creates the axis without touching that.
    /// </para>
    /// <para>
    /// The cost is that Projectile and Aura lose single-target throughput. That is intended and is
    /// repaid in target COUNT once waves hold more than one creature — they are the Spread Forms, and
    /// their value is meant to be how many things they touch, not how hard.
    /// </para>
    /// </remarks>
    // RAISED WITH THE COOLDOWNS (2026-08-26): every casting Form's cooldown grew (Strike ×1.5,
    // Projectile ×1.67, Transformation ×1.4, Trap ×1.33) so the basic attack has room between casts,
    // and each Form's hit grew by the same factor — damage PER COOLDOWN is what it was, in fewer,
    // heavier blows, and the swing is added on top. (Aura ticks and Mark are untouched: no cooldown.)
    public IReadOnlyDictionary<Form, float> FormBaseValue { get; init; } = new Dictionary<Form, float>
    {
        // THE BEAT (2026-08-27): Strike every 3 beats (4.5 s), Projectile every 2 (3 s), Transformation
        // 4 s — and each hit scaled once more so damage per cooldown stays where the 2026-08-26 pass put it.
        // ...and ×2 for the beat's ceiling (measured, not estimated): one action per 1.5 s is 0.67
        // actions/s where the old lock allowed 1.43, so a saturated rotation lost HALF its output — the
        // mastery sweep's geared build died on the wave-30 boss whatever its branch (every Spread node
        // measured 29 deep, hits ×2 measured 39). Scaled so a full rotation lands where the economy, the
        // pacing band and the balance sweeps were tuned. A one-skill build is cadence-bound, not
        // beat-bound, so it gains more than parity from this — the beat's own reward for going deep.
        // ...and again when the cooldowns DOUBLED (2026-08-29). A rhythm skill fires half as often, so a
        // hit grew by about 1.6 — not by 2: the rest is the basic attack's, which now takes five beats in
        // six and grew from 48 to 72. Measured against the rule that a single skill must still beat bare
        // hands decisively (test_the_auto_attack_cannot_carry_a_build) and the pacing band.
        [Form.Trap] = 290f,        // the largest hit in the game, and the rarest — a bite answers, not a beat
        [Form.Strike] = 500f,      // the large-hit workhorse (70 → 105 → 157 → 315 → 500)
        [Form.Transformation] = 260f,  // (40 → 56 → 64 → 130 → 260)
        [Form.Projectile] = 215f,  // pays in targets, not in size (20 → 33 → 66 → 135 → 215)
        [Form.Aura] = 12f,         // smallest of all; it is a field, not a blow
        [Form.Mark] = 0f,          // Mark deals no direct damage — it amplifies other sources.
    };

    public static WeavingTuning Default { get; } = new();
}

/// <summary>An ability the player has woven: Source x Form x Vow.</summary>
public sealed record WovenAbility
{
    public required string Name { get; init; }
    public required Source Source { get; init; }
    public required Form Form { get; init; }
    public Vow? Vow { get; init; }

    /// <summary>
    /// The catalogue id this ability resolves to — the identity that outlives Form. Set by the
    /// composer for every production build since save v3; null only in fixtures that still speak
    /// Form, which resolve through the legacy bridge exactly as before.
    /// </summary>
    public string? SkillId { get; init; }
}

public static class Weaving
{
    /// <summary>Formula 3 — base power, scaled by the Hunter's resonance affinity.</summary>
    public static float BasePower(Form form, float resonanceAffinity, WeavingTuning tuning)
        => tuning.FormBaseValue[form] * (1f + tuning.SourceScalingCoefficient * resonanceAffinity);

    /// <summary>
    /// The legacy full pipeline: base power → Vow → Source matchup. TEST-ONLY since the solo model;
    /// kept while WeavingTests still states the Vow-gating rule through it. Dies with P3c.
    /// </summary>
    public static float AbilityPower(
        WovenAbility ability, Source targetSource, float resonanceAffinity, bool vowActive, WeavingTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(ability);

        var power = BasePower(ability.Form, resonanceAffinity, tuning);

        if (ability.Vow is { } vow)
        {
            var applies = vow.Kind == Builds.VowKind.StaticCost || vowActive;
            if (applies) power *= Builds.Vows.Multiplier(vow);
        }

        return power * SourceMatchup.Effectiveness(ability.Source, targetSource);
    }
}
