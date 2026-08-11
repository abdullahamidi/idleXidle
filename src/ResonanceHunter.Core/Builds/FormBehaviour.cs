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
    public static int BaseCooldownMs(Form form) => form switch
    {
        Form.Strike => 2_000,
        Form.Projectile => 900,          // volume: fires roughly twice per Strike
        Form.Aura => 0,                  // always on — see IsPassive
        Form.Trap => 3_000,              // rare, and only pays when bitten
        Form.Mark => 4_000,              // a window, not a rhythm
        _ => 2_500,                      // Transformation
    };

    /// <summary>An AURA has no cooldown — it ticks for the whole fight. Nothing else may.</summary>
    public static bool IsPassive(Form form) => form == Form.Aura;

    /// <summary>How often a passive Form ticks.</summary>
    public const int AuraTickMs = 500;

    /// <summary>TRAP only pays when the enemy attacks. That is its whole identity.</summary>
    public static bool FiresOnBeingHit(Form form) => form == Form.Trap;

    /// <summary>MARK deals nothing and multiplies everything else instead.</summary>
    public static bool IsAmplifier(Form form) => form == Form.Mark;

    /// <summary>How long a MARK's window lasts.</summary>
    public const int MarkWindowMs = 2_500;

    /// <summary>What a MARK multiplies every other source by while it holds.</summary>
    /// <remarks>
    /// Under half its cooldown, deliberately — the same rule BULWARK needed. An amplifier that is
    /// always up is not an amplifier, it is a damage stat, and it would collapse into "take Mark, take
    /// three Strikes, stop thinking".
    /// </remarks>
    public const float MarkMultiplier = 1.6f;

    /// <summary>TRANSFORMATION returns this fraction of its damage as health.</summary>
    public const float TransformationLeech = 0.5f;

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
    {
        var ring = Enum.GetValues<Form>().Length;                 // 6
        var raw = Math.Abs((int)affinity - (int)skill);
        var dist = Math.Min(raw, ring - raw);                     // cyclic distance 0..3
        // A SHARPER class: committing to a specialisation leans in hard (x2.0 on your Form) and the far
        // Forms fight you more (x0.45 at the opposite). It stays a lean, not a lock — the opposite Form is
        // still castable, so a specialist keeps room to splash one off-Form skill for coverage. Your class
        // is WHAT you're best at; the four woven skills and three keystones are HOW you build within it.
        return dist switch
        {
            0 => 2.00f,   // your mastery — you are a specialist now, and it shows
            1 => 1.15f,   // adjacent — the comfortable neighbours
            2 => 0.75f,   // off-affinity — a real cost
            _ => 0.45f,   // the opposite Form — usable for a splash, but it truly fights you
        };
    }

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
