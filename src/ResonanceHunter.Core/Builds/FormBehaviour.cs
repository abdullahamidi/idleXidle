using System;
using ResonanceHunter.Core.Abilities;

namespace ResonanceHunter.Core.Builds;

/// <summary>
/// What each <see cref="Form"/> actually DOES in a fight.
/// </summary>
/// <remarks>
/// <para>
/// Forms have existed since the Weaving library was written and have never once been read by anything.
/// The Source axis got wired (the region matchup) and the Vow axis got wired (a restriction for power),
/// but Form — the verb, the middle of Source x Form x Vow — was a lookup table of base damage numbers
/// and nothing else. Six Forms that differed only in how hard they hit are not six Forms; they are one
/// Form with six price tags.
/// </para>
/// <para>
/// <b>This is what makes a build a playstyle rather than a stat sheet.</b> Four slots against six
/// Forms is only a decision if the Forms disagree about how to fight. So each one gets a rule that the
/// others cannot copy:
/// </para>
/// <list type="bullet">
/// <item><b>STRIKE</b> — big, honest, immediate. The baseline every other Form is measured against.</item>
/// <item><b>PROJECTILE</b> — weaker per hit, but fires on a short cooldown. Volume over weight.</item>
/// <item><b>AURA</b> — weakest per tick and needs NO cooldown: it is always on. Rewards long fights.</item>
/// <item><b>TRAP</b> — the biggest number in the game, but it only pays when the enemy ATTACKS you.
///   Rewards being hit, which no other Form does.</item>
/// <item><b>MARK</b> — zero damage. Multiplies everything else for a window. Worthless alone, and the
///   best slot in the game next to three big hits.</item>
/// <item><b>TRANSFORMATION</b> — modest damage, and it heals you as it lands. The sustain Form — and
///   the one BLOOD MAGIC turns off entirely, which is what makes that keystone a real refusal.</item>
/// </list>
/// </remarks>
public static class FormBehaviour
{
    /// <summary>
    /// How often a Form comes back, in ms, before any rate modifier.
    /// </summary>
    /// <remarks>
    /// Sized against a 2-4s wave, which is the lesson the skills layer learned the hard way: cooldowns
    /// are measured against WAVE LENGTH, not against each other. The first version of that system used
    /// 5-10s cooldowns and its skills could never fire at all — REND first landed at wave 15 and two
    /// skills never fired in an entire game. If wave length is ever retuned, THESE MOVE WITH IT.
    /// </remarks>
    /// <summary>
    /// A Form's cooldown COUNTED IN BEATS — "every third action" — or 0 when it counts in time
    /// (<see cref="BaseCooldownMs"/>). THE BEAT MODEL (2026-08-27): the champion acts on a metronome
    /// (SoloBattle.DefaultBeatMs ÷ action speed), one cast or one swing per beat. Strike and Projectile
    /// are rhythm skills and count beats, so a fast build casts them sooner in seconds but not in
    /// actions; Transformation and Mark are windows and count seconds; Trap answers a bite; Aura ticks.
    /// </summary>
    /// <remarks>
    /// <b>DELEGATES TO THE CATALOGUE.</b> This table used to hold the numbers, which meant every skill
    /// of a style shared one cadence and a variation could never change it. <see cref="SkillCatalogue"/>
    /// owns them now (design §5), and this stays as the Form-shaped door onto it so the readouts and
    /// the tests that speak Form keep working — and, more importantly, keep agreeing with the fight.
    /// Two counters for one rule is the shape this project keeps paying for.
    ///
    /// A passive skill returns 0: it counts no beats because it takes none.
    /// </remarks>
    public static int CooldownBeats(Form form) => Native(form).Beats;

    /// <summary>
    /// The skill a Form resolves to IN ITS OWN NATURAL KIND — an AURA's field, a STRIKE's blow.
    /// </summary>
    /// <remarks>
    /// Not <c>Resolve(form, passive: false)</c>. That asks for the style's ACTIVE, which is right when
    /// a player has chosen an active slot and wrong here: this table answers "what has this Form always
    /// been", and forcing AURA to its style's active made a field beat-counted and gave it a cast's
    /// cooldown.
    /// </remarks>
    private static SkillDef Native(Form form)
        => SkillCatalogue.Resolve(form, IsPassive(form) || FiresOnBeingHit(form));

    /// <summary>
    /// The time-counted cooldown; for a beat-counted Form this is its beats at action speed 1.0, for
    /// readouts and the wave-length rule (a cooldown must fit a wave).
    /// </summary>
    // DOUBLED 2026-08-27 ("raise the skills' waiting times so the plain hit matters — we can even double
    // them"): a rhythm skill costs twice as many actions, so the basic attack fills most of the beats and
    // TEMPO is felt on the swing rather than only between casts. The hits did NOT double with them — that
    // is the point; the balance was re-measured instead (see WeavingTuning.FormBaseValue).
    //
    // NOT RAISED AGAIN, 2026-08-28, and the attempt is recorded because the next person will have the
    // same idea. The designer asked for longer waits — with four slots filled every beat was somebody's
    // cast and they watched waves with no plain swing in them at all ("düz vuruş imkanı olmadığı senaryo
    // bile yaşanıyor") — and this knob turned out to have no room left in it. THREE invariants, each
    // found by running the suite rather than by reasoning:
    //
    //   * test_cooldowns_are_sized_to_a_wave_not_to_each_other — a cooldown longer than a WAVE can never
    //     fire. Waves run 6-15 s, so the ceiling is 6 beats (9 s). STRIKE ALREADY SITS ON IT.
    //   * test_projectile_trades_weight_for_volume — PROJECTILE is the light Form and must fire more
    //     often than STRIKE. With Strike pinned at the ceiling, Projectile has one step (4->5) and no more.
    //   * ...and that one step made WEAVER (SkillRate 0.70 for a second Form at 45%) a NET LOSS: 17306 ->
    //     16516 damage, i.e. strictly worse than not taking it, because its price was set when casts were
    //     four beats apart. Raising the three windows 8s->9s instead moved gleam/minute to x1.34 against
    //     a 0.80-1.25 band, because ExpeditionTuning.WaveHaulScale is calibrated against clear time.
    //
    // So every available step buys a slower fight by breaking something the fight already promised. The
    // problem is not the size of these numbers: it is that FOUR SKILLS DRAW ON ONE METRONOME, and the only
    // way to give the swing its beats back is for some skills to stop taking one. That is the slot rework
    // the designer wants to design in its own session (2 active + 2 passive; a passive costs no beat, the
    // way Aura does today). Until then the FEEL was improved where it costs no balance — the swing takes a
    // smaller share of the beat and a cast a larger one (see ClipShareOfBeat / SkillClipShareOfBeat).
    /// <remarks>
    /// Also from the catalogue now. An ACTIVE reports its beats as milliseconds at speed 1.0; a
    /// passive reports the clock it actually runs on — a Field its tick interval, a Reaction the
    /// 8 seconds a trap waits to re-arm.
    /// </remarks>
    public static int BaseCooldownMs(Form form)
    {
        var def = Native(form);
        if (def.Beats > 0) return def.Beats * SoloBattle.DefaultBeatMs;
        return def.IntervalMs > 0 ? def.IntervalMs : 8_000;
    }

    /// <summary>
    /// How many creatures one activation of this Form reaches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the other half of the Weight/Spread axis. Enemy armour makes hit SIZE matter; target
    /// count is what makes hit size a TRADE rather than a free upgrade. A Trap hits once for 110 and a
    /// Projectile twice for 20 each — against one armoured creature the Trap is far ahead, and against
    /// five weak ones it spends its whole cooldown killing one of them while the other four keep biting.
    /// </para>
    /// <para>
    /// AURA is a field, not a blow: it reaches everything, which is why its per-hit damage is the
    /// smallest in the game.
    /// </para>
    /// </remarks>
    public static int Targets(Form form) => form switch
    {
        Form.Aura => int.MaxValue,   // a field — everything in the wave
        Form.Projectile => 2,
        _ => 1,                      // Strike, Trap, Transformation; Mark deals no damage at all
    };

    /// <summary>An AURA has no cooldown — it ticks for the whole fight. Nothing else may.</summary>
    public static bool IsPassive(Form form) => form == Form.Aura;

    /// <summary>How often a passive Form ticks.</summary>
    /// <remarks>
    /// 1000, from 500 (playtest 2026-08-28: "aura skillinin tetiklenme süresini 0.5 saniyeden 1 saniyeye
    /// çıkaralım"). Twice as slow and twice as big, so an Aura reads as a PULSE the eye can follow rather
    /// than a blur of small numbers — the wave is long enough now to show four or five of them.
    ///
    /// DAMAGE IS UNCHANGED, by construction rather than by retuning: a tick pays
    /// <c>BaseDamage × (AuraTickMs / 1000)</c>, so the per-tick number scales with the interval and the
    /// damage per second comes out identical. RADIANCE keeps its ratio for the same reason — it shortens
    /// the INTERVAL to three fifths (600 ms) while a tick still pays a full second, so it is worth x1.67
    /// exactly as it was at 500/300.
    ///
    /// Must stay a multiple of <see cref="Expeditions.ExpeditionTuning.TickMs"/> (100), and so must its
    /// RADIANCE three-fifths, or the modulo the tick loop is gated on never lands.
    /// </remarks>
    public const int AuraTickMs = 1000;

    /// <summary>TRAP only pays when the enemy attacks. That is its whole identity.</summary>
    public static bool FiresOnBeingHit(Form form) => form == Form.Trap;

    /// <summary>MARK deals nothing and multiplies everything else instead.</summary>
    public static bool IsAmplifier(Form form) => form == Form.Mark;

    /// <summary>How long a MARK's window lasts.</summary>
    // FOUR BEATS (6000 ms at speed 1.0), was a flat 2500. A MARK opens a window for the casts that
    // follow, and when the cooldowns doubled (2026-08-29) the next cast came 6 or 4 beats later — the
    // window closed before anything could land in it, and the MARK SPECIALIST measured as a node that
    // made a Mark build WEAKER. A window is measured in actions, like the skills it amplifies.
    public const int MarkWindowMs = 4 * SoloBattle.DefaultBeatMs;

    /// <summary>What a MARK multiplies every other source by while it holds.</summary>
    /// <remarks>
    /// Under half its cooldown, deliberately — the same rule BULWARK needed. An amplifier that is
    /// always up is not an amplifier, it is a damage stat, and it would collapse into "take Mark, take
    /// three Strikes, stop thinking".
    /// </remarks>
    public const float MarkMultiplier = 1.6f;

    /// <summary>TRANSFORMATION returns this fraction of its damage as health.</summary>
    /// <remarks>
    /// The number lives in <see cref="HealTuning"/> now (it was a 0.5 constant here, and 0.5 is what
    /// the playtest called broken). This reads the live default so the glossary and every screen
    /// that quotes the Form keep one source; the SIM reads the injected
    /// <c>ExpeditionTuning.Heal</c>, which is the same object unless a test swaps it.
    /// </remarks>
    public static float TransformationLeech => HealTuning.Default.TransformationLeech;

    /// <summary>
    /// The cast clip's authored length in ms at skill rate 1 — eight frames the hunt screen plays across
    /// this window, faster at a higher rate. The sim's one-cast-at-a-time gap is this same number
    /// (SoloBattle.CastGapFor), so the animation and the cast lock are one rule.
    /// </summary>
    public const int CastClipMs = 700;

    /// <summary>
    /// How much of a beat an action's clip fills (windup → contact → recovery); the rest is the settle
    /// back to idle that makes the action's END readable. 0.65 of 1500 ms ≈ the authored 1 s clip.
    /// </summary>
    public const float ClipShareOfBeat = 0.55f;

    /// <summary>
    /// The share of a beat a SKILL's clip fills. Larger than the swing's on purpose: a cast plays SLOWER.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both clips used to take the same 0.65 of a beat, so TEMPO hurried a cast exactly as hard as it
    /// hurried a swing and at a real build's action speed the eight frames of a Transformation went by in
    /// two thirds of a second — read as skipped frames rather than as speed (playtest 2026-08-28: "bazı
    /// noktalarda karakter animasyonunu tam oynatmıyor, sanki frame atlıyormuş gibi").
    /// </para>
    /// <para>
    /// Splitting them says what the designer asked for in one number each: "skill animasyon hızını biraz
    /// daha yavaşlat, Tempo statı düz vuruşun animasyonunu hızlandırsın". The SWING got a smaller share
    /// (0.65 -> 0.55), so it is quicker and leaves more of the beat standing — which is where the pause
    /// between actions comes from; the CAST got a larger one, so it plays close to its authored second.
    /// </para>
    /// <para>
    /// Both are fractions OF THE BEAT, so neither can ever overrun the action after it however fast the
    /// build gets. That is the whole reason the number is a share and not a duration.
    /// </para>
    /// </remarks>
    public const float SkillClipShareOfBeat = 0.90f;

    /// <summary>Is this Form the one BLOOD MAGIC switches off?</summary>
    public static bool Heals(Form form) => form == Form.Transformation;

    /// <summary>
    /// Your AFFINITY, HxH-Nen style: you are a master of ONE Form, strong in its neighbours, weak in
    /// its opposite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the identity axis the playtest asked for — "pick an affinity like the Nen hexagon, then
    /// build skills for it". The six Forms sit on a ring (the enum order IS the ring); your chosen
    /// affinity multiplies a skill by how close its Form is to yours. Committing to your affinity's Form
    /// is rewarded; splashing the opposite Form is a real cost, not a free pick — which is what turns
    /// "weave four skills" into "who am I as a fighter".
    /// </para>
    /// <para>
    /// The opposite is weakened, never zeroed: a Strike specialist CAN still carry a Trap, it just hurts.
    /// A zero would make the choice a lock instead of a lean.
    /// </para>
    /// </remarks>
    public static float AffinityFactor(Form affinity, Form skill)
        => FactorAtDistance(HexDistance(affinity, skill));

    /// <summary>
    /// The affinity factor WITH the Nen buy-back: a SWORN VOW on the skill pulls it one ring toward
    /// your discipline.
    /// </summary>
    /// <remarks>
    /// Straight from the system this game was born from — in Nen, a restriction is how you wield what
    /// your affinity does not give you (the chains were never the conjurer's category). Mechanically:
    /// an off-discipline skill that carries a sworn Vow is judged at one ring closer, so the opposite
    /// Form climbs 0.45 → 0.75, an off-Form 0.75 → 1.15. The native Form gains nothing — you already
    /// own it — and an unsworn skill pays the full distance.
    /// </remarks>
    public static float AffinityFactor(Form affinity, Form skill, bool vowSworn)
    {
        var dist = HexDistance(affinity, skill);
        if (vowSworn && dist > 0) dist -= 1;
        return FactorAtDistance(dist);
    }

    /// <summary>Cyclic distance on the six-Form hexagon, 0..3.</summary>
    private static int HexDistance(Form a, Form b)
    {
        var ring = Enum.GetValues<Form>().Length;                 // 6
        var raw = Math.Abs((int)a - (int)b);
        return Math.Min(raw, ring - raw);
    }

    // A SHARPER class: committing to a specialisation leans in hard (x2.0 on your Form) and the far
    // Forms fight you more (x0.45 at the opposite). It stays a lean, not a lock — the opposite Form is
    // still castable, so a specialist keeps room to splash one off-Form skill for coverage. Your class
    // is WHAT you're best at; the four woven skills and three keystones are HOW you build within it.
    private static float FactorAtDistance(int dist) => dist switch
    {
        0 => 2.00f,   // your mastery — you are a specialist now, and it shows
        1 => 1.15f,   // adjacent — the comfortable neighbours
        2 => 0.75f,   // off-affinity — a real cost
        _ => 0.45f,   // the opposite Form — usable for a splash, but it truly fights you
    };

    /// <summary>
    /// A Form's damage, before Source matchup, Vow and build multipliers.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="WeavingTuning.FormBaseValue"/> — the numbers the Weaving library has carried
    /// since it was written, finally consumed by something.
    /// </remarks>
    public static float BaseDamage(Form form, float resonance, WeavingTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        return Weaving.BasePower(form, resonance, tuning);
    }
}
