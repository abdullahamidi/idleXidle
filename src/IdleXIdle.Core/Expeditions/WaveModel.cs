using System;

namespace IdleXIdle.Core.Expeditions;

/// <summary>
/// Every knob for the wave loop: how threat and reward scale, and how often a boss lands.
/// </summary>
/// <remarks>
/// These were the shared half of the retired squad engine's tuning. The squad-only knobs (exit shares,
/// role mitigation, focus-fire) left with the <c>Expedition</c> class; what remains is what the live
/// single-champion loop (<see cref="IdleXIdle.Core.Builds.SoloExpedition"/>) and its per-wave sim
/// still read.
/// </remarks>
public sealed record ExpeditionTuning
{
    /// <summary>
    /// The basic attack's raw damage at zero MIGHT (default <see cref="Builds.SoloBattle.AutoAttackDamage"/>).
    /// A probe that must isolate one node sets it to 0 — then the champion never swings at all.
    /// </summary>
    public float AutoAttackDamage { get; init; } = Builds.SoloBattle.AutoAttackDamage;

    /// <summary>
    /// THE BEAT, in ms at action speed 1.0 (default <see cref="Builds.SoloBattle.DefaultBeatMs"/>). The champion
    /// acts on the beat and only on the beat — one cast or one swing per beat — and TEMPO shortens it.
    /// </summary>
    public int BeatMs { get; init; } = Builds.SoloBattle.DefaultBeatMs;

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

    /// <summary>
    /// HOW LONG A WAVE LASTS. Multiplies every creature's HEALTH, the CHAMPION'S POOL
    /// (<see cref="Builds.SoloBattle.ChampionHealth"/>) and the wave's GLEAM by the same number —
    /// nothing else. The fight takes this many times longer and costs and pays exactly what it did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE COMPLAINT THIS ANSWERS (playtest 2026-08-28): the fight was over before the player could see
    /// it. Measured at 1.0, a four-skill build cleared a wave in 2.6-3.4 seconds — <b>1.9 to 2.4 champion
    /// actions</b> — against a 2.05 s transition, so more than half the loop was the pause between fights.
    /// A Strike costs six actions, so it fired every second or third wave: a player watching their own
    /// build saw a swing, a swing, and a banner.
    /// </para>
    /// <para>
    /// <b>THIS IS A SIMILARITY TRANSFORM, and it took two wrong ones to find it.</b> Wave length is
    /// <c>enemy health / champion damage</c>, so health is the only lever on it — but health alone makes
    /// the champion eat that many more bites and die far shallower, which
    /// <see cref="Economy.PacingTest"/> had already measured and written down: across a health sweep a
    /// fresh champion's depth fell from wave 8 to wave 4, under the note <i>"raising enemy health does
    /// not lengthen the run — it kills the champion sooner"</i>.
    /// </para>
    /// <para>
    /// The obvious repair — thin the bite by the same factor, holding damage-taken-per-wave fixed — was
    /// measured too, and it is WRONG in a way worth recording, because the arithmetic looks airtight.
    /// Damage DEALT per wave is the wave's health, so it rose 2.5x while damage TAKEN per wave stayed
    /// flat; every heal in the game that pays out of damage dealt (NATURE'S SIGNATURE's leech,
    /// Transformation's, REBOUND) therefore got 2.5x stronger against an unchanged threat. The fight
    /// started healing more than it cost: <c>SoloExpeditionTests</c>' attrition fixture recorded the
    /// champion ENDING wave two with more health than it started (494 -> 496), depth ran from 12 to 23 at
    /// 120 ranks, and the vow sweep stopped discriminating because every branch walked to the cap.
    /// </para>
    /// <para>
    /// So the invariant is not "damage taken per wave" — it is <b>every per-wave quantity against the
    /// pool</b>. Scale the health on both sides and leave all damage alone, and each one lands on 2.5x
    /// together: damage taken (same enemy damage-per-second, 2.5x the seconds), leech (2.5x the damage
    /// dealt), regeneration (2.5x the seconds), the per-wave heal ceiling and between-wave regain (2.5x
    /// the pool they are fractions of), PADDING's flat cut and FORTIFY's free bite (2.5x the bites).
    /// Beat-counted skills and time-counted ones both fire 2.5x per wave, because the beat and the clock
    /// stretch together. Depth, the branch spread and gleam per second all come out where they were.
    /// </para>
    /// <para>
    /// The bite INTERVAL deliberately does not move either. At 1500 ms it is the champion's own beat, so
    /// the wave is now four or five real exchanges — blow for blow on one clock — instead of two.
    /// </para>
    /// <para>
    /// 1.0 is the old game exactly, which is what makes this testable rather than argued. Pass it to
    /// <see cref="Builds.SoloBattle.ChampionHealth"/> as well: the pool is half the transform.
    /// </para>
    /// </remarks>
    public float WaveLengthScale { get; init; } = Builds.SoloBattle.WaveLengthScale;

    /// <summary>
    /// The champion pool's share of <see cref="WaveLengthScale"/> — see
    /// <see cref="Builds.SoloBattle.ChampionPoolScale"/> for why it is smaller and how it was measured.
    /// Carried here so <see cref="Builds.SoloExpedition.RefreshPool"/> re-mints through the SAME number
    /// the run was minted with; a sweep that moves one and not the other measures a game nobody plays.
    /// </summary>
    public float ChampionPoolScale { get; init; } = Builds.SoloBattle.ChampionPoolScale;

    /// <summary>
    /// What one wave pays, as a multiple of what it paid before the wave-length change. Measured
    /// against income per REAL minute, which is the one thing the change must not move.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NOT <see cref="WaveLengthScale"/>, and the difference is the whole reason this is its own number.
    /// The health multiplier is 2.5, but a wave does not become 2.5x longer in wall-clock terms: the
    /// fight itself stretches by about 2.1x (the wave's flat opening pause amortises over more beats —
    /// see <see cref="Builds.SoloBattle.ChampionPoolScale"/>), and the transition it is followed by got
    /// SHORTER on the same day, from 2.05 s to 1.10 s. Paying 2.5 against a loop that grew 1.7 was a
    /// 49% pay rise nobody asked for: the champion went from 192 to 286 Gleam a minute and the lifetime
    /// training grind from 230 hours to 154.
    /// </para>
    /// <para>
    /// Income is linear in this number — the haul never touches the fight — so it was solved for rather
    /// than guessed, against the champion's own faucet with the Warren's fixed 66/minute taken out of
    /// both sides (leaving it in is what made a first attempt at 1.68 land 11% high):
    /// </para>
    /// <code>
    ///   haul x   champion gleam/min   whole economy   lifetime grind   first rank
    ///   before             126             192/min          230.0 h         26 s
    ///     2.50             220             286/min          154.4 h         16 s
    ///     1.68             147             213/min          207.4 h         24 s
    ///     1.44             126             192/min          229.4 h         27 s   &lt;- here
    /// </code>
    /// <para>
    /// Shortening the transition therefore buys a snappier loop and NOT a richer one, which is the
    /// conservative reading of the brief: the economy was already tuned, and this change was about how
    /// long a wave takes.
    /// </para>
    /// </remarks>
    public float WaveHaulScale { get; init; } = 1.44f;


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

    /// <summary>
    /// The skill-healing knobs — leech fractions and the per-wave heal ceiling. See
    /// <see cref="IdleXIdle.Core.Builds.HealTuning"/> for the numbers and the playtest behind them.
    /// </summary>
    /// <remarks>
    /// Carried on the expedition tuning rather than as bare constants so the per-wave sim reads the
    /// INJECTED numbers: a balance probe can run the old and the new healing against the same curve in
    /// one test, which is how the rework was measured rather than argued.
    /// </remarks>
    public IdleXIdle.Core.Builds.HealTuning Heal { get; init; } = IdleXIdle.Core.Builds.HealTuning.Default;

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
public enum BattleEventKind
{
    Strike, EnemyStrike, Down, Heal, EnemyDown, Skill, Shield, Charge, Aura, Beat,

    /// <summary>
    /// UNDYING caught a killing blow. <c>Amount</c> is how long the steel shows, in ms. Its own kind
    /// because <see cref="Shield"/>'s Amount is a banked HEALTH figure — one payload carrying a
    /// duration for one producer and a magnitude for another was a live replay bug.
    /// </summary>
    Undying,

    /// <summary>
    /// A creature's defence was broken. <c>Slot</c> is the creature's index, <c>Amount</c> the number
    /// of breaks it is now carrying.
    /// </summary>
    /// <remarks>
    /// PRESS strips defence every two seconds and the fight showed NOTHING for it — a rule the player
    /// reads on the build screen and can never see happen (playtest: "2 saniyede bir 6 defans kırıyor
    /// diyor ama ben oyunda çok farkedemedim"). A creature's defence is not drawn anywhere, so this
    /// event exists to put a state badge over the one being broken.
    /// </remarks>
    Break,
}

/// <summary>
/// BEAT: one per action the champion takes, carrying <see cref="Builds.Champion.BeatCount"/> in
/// <c>Amount</c> — the run-cumulative count, not a wave-local one.
/// </summary>
/// <remarks>
/// <para>
/// THE RHYTHM IS PUBLISHED NOW RATHER THAN INFERRED. Every rhythm cooldown in the sim is counted in
/// beats (<c>ReadyAtBeat[i] = BeatCount + beats</c>), and the rail that reports those cooldowns had no
/// access to that number — so it reconstructed one, by treating every <see cref="BattleEventKind.Strike"/>
/// that was not a skill's own damage as "an action". That is a heuristic over DAMAGE, and it was wrong
/// for four sources that share the same flag: the poison bleed, THORNS' reflect, BREAKER's overkill
/// spill and a MARK detonation all land with <c>fromSkill: false</c> and would each have turned the
/// dial a notch the champion never earned.
/// </para>
/// <para>
/// Two counters for one rule is the shape this project keeps paying for. This is the rule's own
/// counter, emitted where it is incremented, so a readout can agree with the fight by reading it
/// instead of by guessing well.
/// </para>
/// </remarks>
/// <summary>
/// AURA: one per tick of a passive skill, slot-keyed exactly as
/// <see cref="BattleEventKind.Skill"/> is — but it is NOT an action and
/// raises no cast. Until 2026-08-30 an Aura emitted nothing but its damage, so the screen had to tell
/// its blows from a cast's by their timestamp; at some action speeds a fifth of the ticks collided with
/// a cast and were reported as that cast's, and the skill rail could never answer "when did the Aura
/// last fire" because there was nothing to answer with.
/// </summary>
/// <summary>A single beat of the fight, for the presentation layer to replay.</summary>
/// <param name="Slot">
/// Who the beat is about. For <see cref="BattleEventKind.EnemyStrike"/> and
/// <see cref="BattleEventKind.Heal"/> this is the champion's slot; for
/// <see cref="BattleEventKind.Strike"/> and <see cref="BattleEventKind.EnemyDown"/> it is the INDEX OF
/// THE CREATURE in the wave's composition. For <see cref="BattleEventKind.Skill"/> and
/// <see cref="BattleEventKind.Aura"/> it is the SLOT INDEX of the acting skill in the build — the
/// screen resolves name, art and Source from the equipped skill itself (P3c, 2026-08-31; the old
/// payload was a (Source, Form) ordinal pair, which two skills of one style collided on).
/// </param>
/// <remarks>
/// A wave used to hold one enemy, so every Strike carried slot 0 and the replay could keep a single
/// aggregate health bar. Now that a wave is a composition, the index is what lets the screen show five
/// creatures dying one at a time instead of one bar draining — which is the difference between a player
/// being able to see "I killed two of five" and not.
/// </remarks>
/// <param name="FromSkill">
/// A Strike dealt by a skill (cast, aura tick, trap bite) rather than the auto-attack. The hunt grades
/// its damage numbers by it — an auto-swing landing on the same millisecond as a cast used to print
/// a size up (review 2026-08-26); a timestamp is not a provenance.
/// </param>
public readonly record struct BattleEvent(BattleEventKind Kind, int Slot, int Amount, int AtMs, bool FromSkill = false);

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
/// <see cref="IdleXIdle.Core.Builds.SoloBattle"/>, but the SHAPE of the run — threat compounding
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
