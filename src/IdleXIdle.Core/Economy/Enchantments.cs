using System;
using System.Collections.Generic;
using System.Linq;
using IdleXIdle.Core.Loot;

namespace IdleXIdle.Core.Economy;

/// <summary>
/// An enchantment that changes what HAPPENS, not what a number is.
/// </summary>
/// <remarks>
/// <para>
/// The design asks for effects that change gameplay — "splitting projectiles, spawning a minion after
/// a kill, converting critical hits into poison, increasing production while at low health" — and the
/// codebase could not express a single one of them. Not because nobody had written them: because
/// <see cref="GearMods"/> is a four-float struct, so by construction every gear effect in the game
/// could only ever be a multiplier. The data foreclosed the design.
/// </para>
/// <para>
/// So an enchantment is NOT a number. It is a named trigger the battle asks about at a specific
/// moment: "did anything want to happen when this creature killed something?" A multiplier answers
/// "how much"; these answer "and then what".
/// </para>
/// </remarks>
public enum EnchantKind
{
    /// <summary>
    /// On a kill, the spoils splinter richer — a bump of loot quality on the wave's haul.
    /// </summary>
    /// <remarks>
    /// It was written as "a second, weaker blow at the next wave's enemy", but the single-champion, one-enemy-
    /// per-wave model can't express a blow at a NEXT enemy, so the effect is loot quality on the kill. The
    /// name and blurb now say what it does rather than what the squad model once let it do.
    /// </remarks>
    Splinter,

    /// <summary>On a kill, buds a spare core. The vision's "spawn a minion after a kill", as economy.</summary>
    Harvest,

    /// <summary>A skill cast also poisons: damage that lands over the following seconds.</summary>
    Venom,

    /// <summary>While the wearer is below a third health, the haul swells. Reward for the edge.</summary>
    Desperation,

    /// <summary>On taking a fatal blow, survive at 1 HP. Once per expedition.</summary>
    Undying,

    // ── Form-combo enchantments: these do NOTHING unless your BUILD runs the Form they name. ─────────
    // The whole point of the loot rework — an item is not "a bigger number", it is "this makes my Volley
    // build fire more" or "this makes my Mark build worth committing to". A player without that Form in
    // their skills feels the item is dead, and reaches for the one that fits the build they are building.

    /// <summary>VOLLEY (Projectile) fires one extra time. Dead unless you run Projectile.</summary>
    Overdraw,

    /// <summary>Your MARK's amplify window lasts far longer. Dead unless you run Mark.</summary>
    Linger,

    /// <summary>Your AURA ticks faster — more damage over a long fight. Dead unless you run Aura.</summary>
    Radiance,

    /// <summary>Your STRIKE executes a weakened enemy for far more. Dead unless you run Strike.</summary>
    Execute,

    /// <summary>Your TRAP re-arms far faster, so it punishes every bite. Dead unless you run Trap.</summary>
    Coiled,

    /// <summary>Your TRANSFORMATION leeches far more health. Dead unless you run Transformation.</summary>
    Siphon,

    // ── Keystone- and Vow-combo enchantments. ────────────────────────────────────────────────────────
    // The Form combos above cover one of the build's three axes and all six of its values. These cover
    // the other two. The reasoning is the same and so is the promise: an item is not "a bigger number",
    // it is "this makes the thing I already committed to worth having committed to". The difference is
    // WHICH commitment — a socketed keystone or a sworn Vow rather than an equipped skill.
    //
    // Every one of these is dead on its own, and deliberately so. The bonus is read INSIDE the branch
    // its partner opens, so an unpaired one is not a smaller bonus, it is no bonus.

    /// <summary>BLOODLUST bites harder the closer you are to death. Dead without BLOODLUST.</summary>
    Fervour,

    /// <summary>ECHO's doubled cast hits for more. Dead without ECHO.</summary>
    Reverb,

    /// <summary>ZEAL rewards staying whole even more. Dead without ZEAL (JUGGERNAUT).</summary>
    Bulwark,

    /// <summary>Every Vow you have sworn pays damage. Dead with no Vow sworn.</summary>
    Tithe,
}

/// <summary>
/// What a combo enchantment needs in your build before it does anything at all.
/// </summary>
/// <remarks>
/// The Form combos could say this with a single <c>Form?</c>, and did. Keystone and Vow combos cannot:
/// one asks about a socketed keystone, one asks whether any Vow is sworn, and the Forge has to be able
/// to tell the player which. Carrying the LABEL here rather than deriving it at the draw site keeps the
/// wording next to the rule it describes — "NEEDS BLOODLUST IN YOUR BUILD" is not something a
/// <c>BuildTrigger</c> enum name should have to spell correctly by accident.
/// </remarks>
public sealed record EnchantNeed(
    string Label,
    Builds.Style? Style = null,
    Builds.SkillKind? Kind = null,
    bool AnyAmplify = false,
    Builds.BuildTrigger? Keystone = null,
    bool AnyVow = false)
{
    /// <summary>
    /// The SKILL half of the requirement, against the build's woven defs — which is what the sim
    /// actually gates on (P11): RADIANCE pays any Field-KIND tick (a PRESS build's included) and
    /// LINGER any amplify window, so a need keyed on the FIELD/SIGN styles greyed live combos.
    /// </summary>
    public bool MetBySkills(IReadOnlyCollection<Builds.SkillDef> woven)
    {
        ArgumentNullException.ThrowIfNull(woven);
        return (Style is not { } s || woven.Any(d => d.Style == s))
               && (Kind is not { } k || woven.Any(d => d.Kind == k))
               && (!AnyAmplify || woven.Any(d => d.AmplifyPercent > 0f || d.AmplifyMs > 0));
    }

    /// <summary>Is this requirement satisfied by the build the player is actually running?</summary>
    /// <remarks>
    /// It lives in Core rather than beside the screen that draws the answer, for two reasons. Two
    /// screens ask it — the Forge's loot grid and its detail panel — and a three-clause predicate
    /// copied per call site is how one of them ends up saying COMBOS YOUR BUILD while the other says
    /// nothing. And a rule that lives in a Draw method cannot be unit-tested at all; here it can, and
    /// is. The clauses are ANDed, but each enchantment sets exactly one, so in practice this asks the
    /// single question that enchantment cares about.
    /// </remarks>
    public bool MetBy(IReadOnlyCollection<Builds.SkillDef> woven,
                      IReadOnlyCollection<Builds.BuildTrigger> triggers,
                      int swornVows)
    {
        ArgumentNullException.ThrowIfNull(triggers);

        return MetBySkills(woven)
               && (Keystone is not { } k || triggers.Contains(k))
               && (!AnyVow || swornVows > 0);
    }
}

/// <summary>An enchantment on a specific item, at a specific strength.</summary>
public sealed record Enchantment(EnchantKind Kind, float Magnitude)
{
    public string Name => Kind.ToString().ToUpperInvariant();

    /// <summary>
    /// What it does, in the player's words. States the TRIGGER, because the trigger IS the effect —
    /// and where the sentence carries a NUMBER, it says what the number is of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ONE sentence per enchant, printed everywhere the enchant's effect is shown: the item card
    /// (wrapped, no cap), the Gear inspector (up to four Body lines), the Forge's enchant band (two
    /// Secondary lines). It used to be two sentences — a 21-character fragment for the Forge's old
    /// fixed column and a longer one for the card — and the fragments had lost the half the number
    /// was about: "BLOODLUST +50%" and "ZEAL +50%" said a keystone's name beside a percentage of
    /// nothing, "PER VOW: +10%" named no stat at all, "SKILLS POISON +62%" read as a stat called
    /// POISON (2026-09-07). The Forge's RE-ROLL list still prints candidates by name alone.
    /// </para>
    /// <para>
    /// Each numeric line names the mechanic its figure moves, and the figure is THE ONE THE FIGHT
    /// READS, not the raw magnitude: VENOM's share of a skill hit is the larger of the sim's own floor
    /// and the worn magnitude (<c>SoloBattle.VenomBasePoison</c>), so a Rare and an Epic both poison
    /// for 50% and say so; FERVOUR and BULWARK add to BLOODLUST's and ZEAL's per-health slope, and the
    /// line prints what that is WORTH over the keystone alone at the end of the ramp
    /// (<c>Magnitude / (1 + BloodlustScale)</c>: a Legendary's +0.5 on a 0.8 slope is +28% more
    /// damage at empty health, not "+50%"); REVERB multiplies every hit, the basic swing included,
    /// while ECHO is socketed; TITHE is a damage share per sworn Vow; DESPERATION's threshold is a
    /// third of maximum health (<c>SoloExpedition</c>); SIPHON's two figures are HealTuning's. A line
    /// that only names the keystone and a number is the item version of "GLOVES +2%".
    /// </para>
    /// <para>
    /// Invariant culture on every arm, like <see cref="ItemModifiers.Value(AffixStat, float, bool)"/>:
    /// the copy is English, and a test host or a tool thread on another locale must print the string
    /// the game prints.
    /// </para>
    /// </remarks>
    public string Blurb => Kind switch
    {
        EnchantKind.Splinter => "ON KILL: RICHER LOOT",
        EnchantKind.Harvest => Inv($"ON KILL: {Magnitude * 100f:0}% CHANCE OF A SPARE CORE"),
        EnchantKind.Venom => Inv($"SKILL HITS POISON FOR {MathF.Max(Builds.SoloBattle.VenomBasePoison, Magnitude) * 100f:0}% OF THE HIT"),
        EnchantKind.Desperation => Inv($"BELOW A THIRD HEALTH: +{Magnitude * 100f:0}% LOOT"),
        EnchantKind.Overdraw => "VOLLEY FIRES ONE MORE TIME",
        EnchantKind.Linger => "MARKS LAST LONGER",
        EnchantKind.Radiance => "FIELDS TICK FASTER",
        EnchantKind.Execute => "HAMMER CRUSHES WEAK",
        EnchantKind.Coiled => "SNARES RE-ARM SOONER",
        // Both halves from HealTuning, so the card cannot drift from the sim: the leech multiplier and
        // the raised per-wave healing limit (the half that is measurable under the ceiling). The
        // style it needs (DRAIN) is on the Need row and opens the sentence.
        EnchantKind.Siphon => Inv($"DRAIN HEALS {Times(Builds.HealTuning.Default.SiphonMultiplier)}, UP TO {Builds.HealTuning.Default.CeilingText(siphon: true)} HEALTH A WAVE"),
        EnchantKind.Fervour => Inv($"BLOODLUST: UP TO +{Magnitude / (1f + Builds.SoloBattle.BloodlustScale) * 100f:0}% MORE DAMAGE AT LOW HEALTH"),
        EnchantKind.Reverb => Inv($"WITH ECHO: +{Magnitude * 100f:0}% DAMAGE"),
        EnchantKind.Bulwark => Inv($"ZEAL: UP TO +{Magnitude / (1f + Builds.SoloBattle.ZealScale) * 100f:0}% MORE DAMAGE AT FULL HEALTH"),
        EnchantKind.Tithe => Inv($"+{Magnitude * 100f:0}% DAMAGE PER SWORN VOW"),
        _ => "SURVIVE DEATH ONCE",
    };

    private static string Inv(FormattableString s) => FormattableString.Invariant(s);

    /// <summary>"TWICE AS MUCH" for a multiplier of two, "3 TIMES AS MUCH" otherwise — the leech figure in words.</summary>
    private static string Times(float multiplier)
        => MathF.Abs(multiplier - 2f) < 0.01f ? "TWICE AS MUCH" : Inv($"{multiplier:0} TIMES AS MUCH");

    /// <summary>Does the sentence carry a figure?</summary>
    /// <remarks>
    /// The binary enchants (UNDYING, the Form combos) are switches: rarity buys them as drops, not as
    /// a bigger percentage, and their line rightly has no figure in it. SIPHON is a switch too — its
    /// two figures are HealTuning's, not its magnitude — but they are figures a player reads, so it
    /// counts here. The test that holds every numeric line to naming its mechanic reads this so it
    /// never demands a number of a switch.
    /// </remarks>
    public bool BlurbIsNumeric => Blurb.Any(char.IsDigit);

    /// <summary>
    /// What this enchantment needs in your build to matter — null for the ones that always work.
    /// </summary>
    /// <remarks>The Forge greys the blurb when the worn build lacks it, so a dead combo reads as dead.</remarks>
    public EnchantNeed? Needs => Kind switch
    {
        EnchantKind.Overdraw => new EnchantNeed("VOLLEY", Style: Builds.Style.Volley),
        // KIND and EFFECT, not style, for the two whose sim gate never asked about style (P11):
        // Radiance speeds ANY Field's tick — hammer_press's as much as field_mire's — and
        // Linger stretches ANY amplify window. The style-keyed need greyed those live combos.
        EnchantKind.Linger => new EnchantNeed("A SKILL THAT MARKS", AnyAmplify: true),
        EnchantKind.Radiance => new EnchantNeed("A FIELD SKILL", Kind: Builds.SkillKind.Field),
        EnchantKind.Execute => new EnchantNeed("HAMMER", Style: Builds.Style.Hammer),
        EnchantKind.Coiled => new EnchantNeed("SNARE", Style: Builds.Style.Snare),
        EnchantKind.Siphon => new EnchantNeed("DRAIN", Style: Builds.Style.Drain),
        EnchantKind.Fervour => new EnchantNeed("BLOODLUST", Keystone: Builds.BuildTrigger.Bloodlust),
        EnchantKind.Reverb => new EnchantNeed("ECHO", Keystone: Builds.BuildTrigger.Echo),
        EnchantKind.Bulwark => new EnchantNeed("ZEAL", Keystone: Builds.BuildTrigger.Zeal),
        EnchantKind.Tithe => new EnchantNeed("A SWORN VOW", AnyVow: true),
        _ => null,
    };

}

/// <summary>
/// Which enchantment an item carries, and how strong.
/// </summary>
/// <remarks>
/// Derived from the InstanceId like <see cref="GearTraits"/>, and for the same reasons: nothing to
/// store, no migration, and every item already in a stash grows one. A DIFFERENT hash salt from the
/// trait, though — sharing one would correlate the two axes permanently, so every HEAVY weapon in the
/// game would carry the same enchantment and two axes would collapse back into one.
/// </remarks>
public static class Enchantments
{
    /// <summary>Only rare and better are enchanted. A Common must stay a Common.</summary>
    public const Rarity MinimumRarity = Rarity.Rare;

    // Each slot leans a way: weapons hit, charms keep you alive, focuses SHAPE your skills — which is
    // where the Form-combo enchantments live, because that is exactly what a Focus is for. The
    // KEYSTONE and VOW combos went to the other two slots rather than joining them, so each slot
    // answers a different one of the build's three axes and the Focus does not become the only slot
    // worth chasing.
    //
    // Every pool size is COPRIME TO 4 ON PURPOSE (here: 5, 5, 5). There are 4 traits, and the enchant is
    // picked with `hash % poolLen` off the same InstanceId as the trait (different salt). FNV's low bits
    // are weak, so a pool of 4 — sharing the factor 2 with the 4 traits — would make `%4` here track `%4`
    // there, collapsing the two axes into one (test_the_enchantment_does_not_correlate_with_the_trait
    // catches it). 5 is coprime to 4 and uses more of the hash, so the axes stay independent. A pool of
    // 4 or 6 would re-correlate them — grow a pool to 5 or 7, NEVER to an even number. That constraint
    // is why the two pools below grew by TWO each and not by one.
    private static readonly EnchantKind[] WeaponPool =
    {
        EnchantKind.Splinter, EnchantKind.Venom, EnchantKind.Harvest,
        // The weapon is what swings, so it carries the two keystones that change how hard a swing lands.
        EnchantKind.Fervour, EnchantKind.Reverb,
    };
    // The CHARM keeps you alive, so it carries the sustain combo (SIPHON — Transformation's extra leech)
    // alongside the two survival triggers — and now the two commitments that are about what you have
    // PROMISED rather than what you have equipped: ZEAL's reward for staying whole, and the Vow tithe.
    private static readonly EnchantKind[] CharmPool =
    {
        EnchantKind.Undying, EnchantKind.Desperation, EnchantKind.Siphon,
        EnchantKind.Bulwark, EnchantKind.Tithe,
    };
    // The FOCUS shapes your SKILLS, so the skill-cadence combos live here — one per demand the
    // build can answer (extra shot, longer window, faster ticks, a finisher, quicker snares).
    // That is the slot that turns "I run Fields" into "my Field build actually works".
    private static readonly EnchantKind[] FocusPool =
        { EnchantKind.Linger, EnchantKind.Radiance, EnchantKind.Overdraw, EnchantKind.Execute, EnchantKind.Coiled };

    public static EnchantKind[] PoolFor(GearSlot slot) => slot switch
    {
        GearSlot.Weapon => WeaponPool,
        // Jewellery keeps you going, like a charm; armour and the Focus shape your SKILLS' cadence.
        GearSlot.Charm or GearSlot.Ring => CharmPool,
        _ => FocusPool,   // Focus, Helm, Chest, Gloves, Boots — the skill-combo pool
    };

    public static Enchantment? Of(ItemInstance? item)
    {
        if (item is null || item.Rarity < MinimumRarity) return null;
        if (Gear.SlotFor(item.BaseType) is not { } slot) return null;

        // A REFORGE overrides the id-derived enchantment. The MinimumRarity gate above still runs first,
        // so a stray override on a sub-Rare item reads as no enchantment — which is correct, a Common
        // has none. The magnitude is NOT stored; it is always recomputed from rarity below.
        if (item.EnchantOverride is { } forced)
            return new Enchantment(forced, MagnitudeFor(forced, item.Rarity));

        var pool = PoolFor(slot);
        var hash = Fnv1a(item.InstanceId + "|ench");   // salted — see the class remarks
        var kind = pool[(int)(hash % (uint)pool.Length)];

        return new Enchantment(kind, MagnitudeFor(kind, item.Rarity));
    }

    /// <summary>
    /// How strong, by rarity. Chances stay well under certainty — a trigger that always fires is a stat.
    /// </summary>
    public static float MagnitudeFor(EnchantKind kind, Rarity rarity)
    {
        // Rare 1.20 → Legendary 7.00 on the shared curve, damped hard: these are not multipliers, they
        // are probabilities and fractions, and a 700% chance is nonsense.
        var t = Gear.RarityPower(rarity);

        return kind switch
        {
            EnchantKind.Splinter => Math.Min(0.60f, 0.15f + 0.05f * t),
            EnchantKind.Harvest => Math.Min(0.50f, 0.10f + 0.04f * t),
            EnchantKind.Venom => Math.Min(0.75f, 0.20f + 0.06f * t),
            EnchantKind.Desperation => Math.Min(1.00f, 0.20f + 0.08f * t),

            // The KEYSTONE and VOW combos scale with rarity, unlike the Form combos below, because they
            // sharpen a coefficient rather than switch a behaviour on. "The Volley fires again" has no
            // half-measure; "BLOODLUST bites harder" has nothing BUT degree, so a Legendary has to say a
            // bigger number or rarity means nothing on these four. They are capped well short of their
            // partner's own coefficient — an item that doubles a keystone is no longer an item.
            EnchantKind.Fervour or EnchantKind.Bulwark => Math.Min(0.50f, 0.10f + 0.06f * t),
            EnchantKind.Reverb => Math.Min(0.30f, 0.05f + 0.035f * t),
            // PER VOW, and a build can hold four, so this is quartered against the others by hand.
            EnchantKind.Tithe => Math.Min(0.10f, 0.025f + 0.012f * t),

            // UNDYING and the Form-combo enchantments are BINARY — the Volley either fires again or it
            // doesn't. Rarity buys them as drops (rarer = likelier to roll), not as a bigger percentage.
            _ => 1f,
        };
    }

    /// <summary>Every enchantment currently worn, across all three slots.</summary>
    public static IReadOnlyList<Enchantment> Worn(params ItemInstance?[] worn)
    {
        ArgumentNullException.ThrowIfNull(worn);
        return worn.Select(Of).OfType<Enchantment>().ToList();
    }

    /// <summary>Does the wearer carry this trigger, and at what strength? 0 when absent.</summary>
    public static float MagnitudeOf(IReadOnlyList<Enchantment> worn, EnchantKind kind)
    {
        ArgumentNullException.ThrowIfNull(worn);

        // Sum rather than max: two Harvest sources should both pay. Nothing in the pools can stack more
        // than three deep, so this cannot run away.
        return worn.Where(e => e.Kind == kind).Sum(e => e.Magnitude);
    }

    private static uint Fnv1a(string s)
    {
        var hash = 2166136261u;
        foreach (var ch in s)
        {
            hash ^= ch;
            hash *= 16777619u;
        }
        return hash;
    }
}
