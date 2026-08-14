using System;
using System.Collections.Generic;
using System.Linq;
using ResonanceHunter.Core.Abilities;
using ResonanceHunter.Core.Automation;
using ResonanceHunter.Core.Expeditions;

namespace ResonanceHunter.Core.Builds;

/// <summary>Live state for ONE character across a whole expedition. Health and cooldowns persist.</summary>
public sealed class Champion
{
    public required int MaxHealth { get; init; }
    public int Health { get; set; }
    public bool Alive => Health > 0;

    /// <summary>Ready-at time per skill index, in expedition-absolute ms.</summary>
    public Dictionary<int, int> ReadyAt { get; } = new();

    /// <summary>Expedition-absolute ms elapsed before the current wave. See SkillClock for why.</summary>
    public int ElapsedMs { get; set; }

    /// <summary>UNDYING is once per EXPEDITION, so it lives here and not in a wave-scoped local.</summary>
    public bool UndyingSpent { get; set; }

    /// <summary>Absolute ms until which a MARK is amplifying. 0 = none.</summary>
    public int MarkUntilMs { get; set; }
}

/// <summary>
/// One enemy in a wave. A wave holds between one and five of these.
/// </summary>
/// <remarks>
/// <para>
/// Waves used to be a single float of health, which meant a build could only ever be too SMALL, never
/// the wrong SHAPE — there was nothing for target count to be about and no way for an archetype to
/// punish anything. Everything the redesign asks of content (Swarm, Armoured, Caster, Bruiser) is
/// expressed here: count, health, damage, and flat armour.
/// </para>
/// <para>
/// <see cref="Source"/> is per creature, not per wave. A composition draws mixed Sources so that
/// picking a Source is a bet on the wave's weighting rather than a lookup with one correct answer.
/// </para>
/// </remarks>
public sealed class WaveCreature
{
    public required float MaxHealth { get; init; }
    public float Health { get; set; }

    /// <summary>What this creature deals per bite, before the champion's mitigation.</summary>
    public required float Damage { get; init; }

    /// <summary>
    /// FLAT per-hit mitigation — see <see cref="SoloBattle.MinHitFraction"/>.
    /// </summary>
    /// <remarks>
    /// Settable, not init-only, because SUNDER strips armour permanently FOR THE REST OF THE WAVE. That
    /// is the whole node: a Weight build gets stronger as an Armoured wave goes on, which is the opposite
    /// of how flat mitigation normally behaves. Creatures are minted per wave, so the mutation cannot
    /// leak into the next one.
    /// </remarks>
    public float Defense { get; set; }

    public Source? Source { get; init; }

    /// <summary>What this creature IS — read by SIEGE, which pays against Armoured and taxes the rest.</summary>
    public Encounters.Archetype? Archetype { get; init; }

    public bool Alive => Health > 0f;

    public static WaveCreature Single(float health, float damage, float defense = 0f, Source? source = null)
        => new() { MaxHealth = health, Health = health, Damage = damage, Defense = defense, Source = source };
}

/// <summary>
/// What one wave actually did, measured while it resolved.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the player cannot watch the fight and change anything. With no in-run decisions
/// the run is a measurement, and the ONLY moment they can learn is the report afterwards — so the sim
/// has to record the facts that point at a lever. Every field here maps to something the player can
/// change: absorbed damage points at hit size, targets-per-activation points at action economy, health
/// lost points at sustain.
/// </para>
/// <para>
/// <see cref="RawDamage"/> is measured BEFORE enemy armour and <see cref="DeliveredDamage"/> after, and
/// the gap between them is the single most useful number in the game — it is the difference between
/// "my build is too small" and "my build is the wrong shape", which is exactly the distinction the
/// design says the player must never be unable to make.
/// </para>
/// </remarks>
public sealed class WaveMetrics
{
    /// <summary>Damage swung, before enemy armour ate any of it.</summary>
    public float RawDamage { get; set; }

    /// <summary>Damage that actually landed.</summary>
    public float DeliveredDamage { get; set; }

    /// <summary>Individual hits landed, of any kind.</summary>
    public int Hits { get; set; }

    /// <summary>Skill activations — one per cast, however many creatures it reached.</summary>
    public int Activations { get; set; }

    /// <summary>Creatures struck, summed across activations. Divided by Activations this is reach.</summary>
    public int TargetsStruck { get; set; }

    /// <summary>Creatures present when the wave began.</summary>
    public int CreaturesPresent { get; set; }

    public int CreaturesKilled { get; set; }

    /// <summary>Health the champion lost during this wave.</summary>
    public int HealthLost { get; set; }

    public int DurationMs { get; set; }

    /// <summary>How much of the swing enemy armour ate. The Weight axis, as one number.</summary>
    public float AbsorbedFraction => RawDamage <= 0f ? 0f : 1f - DeliveredDamage / RawDamage;

    public float AverageHitSize => Hits <= 0 ? 0f : DeliveredDamage / Hits;

    /// <summary>Creatures reached per cast. The Spread axis, as one number.</summary>
    public float TargetsPerActivation => Activations <= 0 ? 0f : TargetsStruck / (float)Activations;
}

/// <summary>
/// The fight, for a single character. No squad, no slots, no roles.
/// </summary>
/// <remarks>
/// <para>
/// The roster is gone: the player has ONE character and a BUILD, and every decision that shapes this
/// fight was made before it started — which skills were woven, which keystones were accepted, what is
/// worn. The fight itself is fully automatic. That is the design: the player deals with items and
/// builds, and the game plays itself.
/// </para>
/// <para>
/// Deterministic given a seeded Random, so a build can be reasoned about rather than tested by feel.
/// Pure Core — no MonoGame (ADR-001). It emits <see cref="BattleEvent"/>s for the view to replay.
/// </para>
/// </remarks>
public static class SoloBattle
{
    /// <summary>How hard the character swings without a skill. Deliberately small.</summary>
    /// <remarks>
    /// The auto-attack exists so a fight cannot stall to zero, not so it can carry a build. If it ever
    /// out-damages the woven skills, the skill tree stops mattering and the game is idle-clicker mush.
    /// </remarks>
    public const float AutoAttackDamage = 6f;
    public const int AutoAttackIntervalMs = 1_200;

    /// <summary>
    /// VENOM's poison, as a fraction of the skill hit that inflicts it, for a source that carries no
    /// magnitude of its own — i.e. the VENOMANCER keystone. A worn Venom enchant raises it to its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This constant is the whole reason VENOM stopped being a lie. The trigger existed, VENOMANCER
    /// granted it and PAID −20% damage for it, and nothing in the sim applied a single point of poison —
    /// so the keystone was a pure downside and the weapon enchant was decoration. A dormant trigger,
    /// exactly the bug species this codebase keeps rediscovering.
    /// </para>
    /// <para>
    /// It was 0.30, and at 0.30 VENOMANCER was STILL a trap: the poison recovered most of the −20%
    /// damage but not all of it, so a venomancer build did 4021 to a plain build's 4039 over the full
    /// wave ceiling — a strict downgrade, the exact thing the keystone's "worse early, better late"
    /// promise forbids. Raised to 0.50 so the DoT clearly out-earns the penalty on a long fight
    /// (~+12.6% at the ceiling) while still ramping from nothing, so it stays a trade and not a stat.
    /// The lever is deliberately the poison magnitude, not the penalty: the penalty is the keystone's
    /// identity, and SoloBattleTests isolates the poison by holding an equal-penalty twin against it.
    /// </para>
    /// </remarks>
    public const float VenomBasePoison = 0.50f;

    /// <summary>How much of the standing poison bleeds every half-second. The rest carries to later ticks.</summary>
    public const float VenomBleedPerHalfSecond = 0.5f;

    // ── Commander-stat tuning. CRIT (chance), FOCUS (crit damage) and DEFENSE (mitigation) were three
    //    trained stats that reached NO formula after the pivot to the solo model — the Character screen's
    //    damage bench showed them moving nothing. They are read here now, so training them is real. ──────

    /// <summary>A critical skill hit's floor multiplier, before FOCUS raises it.</summary>
    public const float CritBaseMultiplier = 1.5f;

    /// <summary>Each point of FOCUS adds this to the crit multiplier — FOCUS is the crit-DAMAGE stat.</summary>
    public const float FocusCritDamagePerPoint = 0.01f;

    /// <summary>DEFENSE mitigates a bite on a diminishing curve: taken × K / (K + defense). Never 100%.</summary>
    public const float DefenseMitigationConstant = 100f;

    /// <summary>
    /// The floor under a hit after ENEMY armour. Armour subtracts a flat amount per hit; this stops it
    /// subtracting everything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enemy mitigation is FLAT and per-hit, unlike the champion's own <see cref="DefenseMitigationConstant"/>
    /// curve, and the difference is the entire reason the Armoured archetype can exist. A multiplicative
    /// curve scales every hit by the same factor, so it is completely indifferent to whether damage arrives
    /// as ten hits of 40 or one hit of 400. Under it, "few large hits" and "many small hits" are the same
    /// build and there is nothing for a Weight branch to be about.
    /// </para>
    /// <para>
    /// Flat subtraction makes hit SIZE the thing armour reads. Against 25 armour, ten hits of 40 deliver
    /// 150 and one hit of 400 delivers 375 — same raw damage, 2.5x the result.
    /// </para>
    /// </remarks>
    public const float MinHitFraction = 0.15f;

    // ── Form-combo enchantment tuning. Each effect only fires for its Form, so the item is dead weight
    //    on a build that doesn't run that Form — the whole point of a combo. See Enchantments.NeedsForm. ──

    /// <summary>EXECUTE — a STRIKE against an enemy below this fraction of its max HP hits far harder.</summary>
    public const float ExecuteThreshold = 0.30f;

    /// <summary>How much harder an EXECUTE-boosted STRIKE lands on a weakened enemy.</summary>
    public const float ExecuteMultiplier = 1.6f;

    /// <summary>COILED shortens the TRAP's cooldown by this factor, so it answers far more bites.</summary>
    public const float CoiledCooldownFactor = 0.5f;

    /// <summary>SIPHON multiplies TRANSFORMATION's on-hit leech.</summary>
    public const float SiphonLeechMultiplier = 2.0f;

    /// <summary>HOARDER — how much of the haul multiplier above 1 becomes hit size.</summary>
    public const float HoarderHaulToForce = 0.20f;

    /// <summary>WEAVER — the share of a hit its woven second Form delivers.</summary>
    public const float WeaverEchoFraction = 0.45f;

    /// <summary>
    /// Resolve one wave. Terminates on a kill, a death, or the tick ceiling — never hangs.
    /// </summary>
    /// <summary>
    /// Resolve a wave holding ONE enemy. Kept because most of the game and its tests describe a wave
    /// that way; it builds a single-creature composition and calls the real overload.
    /// </summary>
    public static (WaveOutcome Outcome, List<BattleEvent> Events) ResolveWave(
        Champion champ,
        Build build,
        Economy.Hunter hunter,
        float enemyHealth,
        float enemyDamage,
        int enemyIntervalMs,
        ExpeditionTuning tuning,
        Random rng,
        WaveBonus? bonus = null,
        Source? enemySource = null,
        bool isBoss = false,
        float enemyDefense = 0f)
        => ResolveWave(
            champ, build, hunter,
            new[] { WaveCreature.Single(enemyHealth, enemyDamage, enemyDefense, enemySource) },
            enemyIntervalMs, tuning, rng, bonus, isBoss);

    /// <summary>
    /// Resolve one wave against a composition. Terminates on a clear, a death, or the tick ceiling.
    /// </summary>
    public static (WaveOutcome Outcome, List<BattleEvent> Events) ResolveWave(
        Champion champ,
        Build build,
        Economy.Hunter hunter,
        IReadOnlyList<WaveCreature> creatures,
        int enemyIntervalMs,
        ExpeditionTuning tuning,
        Random rng,
        WaveBonus? bonus = null,
        bool isBoss = false,
        WaveMetrics? metrics = null,
        float sustain = 1f)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        if (creatures.Count == 0) throw new ArgumentException("A wave needs at least one creature.", nameof(creatures));
        ArgumentNullException.ThrowIfNull(champ);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(rng);

        var events = new List<BattleEvent>();
        var mods = build.Resolve(hunter);

        // THE SKILL TREE'S SHAPE. Everything below that reads `shape` is a node in MasteryCatalog; if a
        // field of it stops being read here, its nodes must be deleted, because a node that does nothing
        // is the failure that left the whole loot-rarity chain inert for the length of development.
        var shape = build.Shape;

        // The Vow context is a property of the BUILD, so it is built once and every skill of every tick
        // is judged against the same one. See DescribeBuild.
        var weaveCtx = DescribeBuild(build, hunter);

        // Per-wave state the shape's conditional nodes need. All of it is local, so nothing leaks into
        // the next wave — which matters most for SUNDER, whose armour strip is explicitly wave-scoped.
        var struckOnce = new HashSet<WaveCreature>();   // FOLLOW THROUGH / OPENER / ALPHA
        var cascadeArmed = false;                       // CASCADE
        var assassinated = false;                       // ASSASSINATE — once per wave
        var firstBiteTaken = false;                     // FORTIFY
        var triggers = build.Triggers(hunter);
        var skills = build.Skills;
        var wt = WeavingTuning.Default;
        var resonance = hunter.ValueOf(Economy.HunterStat.ResonanceAffinity);

        // VENOM — how much of each skill hit lingers as poison. Zero unless the build carries the trigger
        // (VENOMANCER, or a Venom weapon); a worn enchant sets its own strength, a bare keystone the base.
        var venomFrac = triggers.Contains(BuildTrigger.Venom)
            ? Math.Max(VenomBasePoison,
                       Economy.Enchantments.MagnitudeOf(hunter.WornEnchantments, Economy.EnchantKind.Venom))
            : 0f;
        var poison = 0f;   // standing poison damage yet to bleed out

        // HARVEST reads its WORN magnitude now, like Venom does — so a rarer Harvest procs a spare core more
        // often, and the tooltip's rarity-scaled % is the truth instead of decoration (the sim used a flat 25%,
        // so a Legendary blurb promising 38% delivered 25%). A bare trigger with no worn enchant keeps a floor.
        var harvestChance = triggers.Contains(BuildTrigger.Harvest)
            ? Math.Max(0.10f, Economy.Enchantments.MagnitudeOf(hunter.WornEnchantments, Economy.EnchantKind.Harvest))
            : 0f;

        // CRIT × FOCUS, folded to a deterministic expected-value factor on SKILL damage (chance × extra),
        // so the sim stays reproducible — no rng draw, no crit-lottery variance to break a seeded test.
        // DEFENSE (including the worn charm's, itself long inert) mitigates each incoming bite below.
        var critChance = Math.Clamp(
            (hunter.ValueOf(Economy.HunterStat.CriticalChance) + hunter.AffixTotal(Economy.AffixStat.Crit)
             + shape.BonusCritPercent) / 100f,
            0f, 0.75f);
        var critMult = CritBaseMultiplier + hunter.ValueOf(Economy.HunterStat.Focus) * FocusCritDamagePerPoint;
        var critFactor = 1f + critChance * (critMult - 1f);
        var defenseFactor = DefenseMitigationConstant / (DefenseMitigationConstant + hunter.Defense);

        // VOW COSTS. A Vow that grants power must charge for it, or it is a stat with good PR. FRAGILITY is
        // paid as extra damage taken — the product across every worn Vow that names a damage-taken price.
        // (RECKLESS OFFERING's price is health, charged once at champion mint via VowHealthMultiplier, so it
        // is deliberately NOT billed here — doing both would bill the same effective-HP twice.) The Vow's
        // BENEFIT is applied per-skill in VowFactor; this is the other half, which the solo model had lost
        // when the squad engine that used to charge it was retired.
        // ONCE PER VOW, not once per skill wearing it. A Vow is sworn, not equipped: FRAGILITY's card
        // says "+12.5% damage taken" and a build that wove it onto four skills was charged 1.125^4 =
        // +60%, because the price compounded per skill while the BENEFIT did not — a skill's Vow bonus
        // is a multiplier on that skill, so four skills at x1.33 is still x1.33 of damage, never x1.33^4.
        // The asymmetry made both static-cost Vows strictly negative to swear, which is what the balance
        // sweep found: FRAGILITY -1 depth, RECKLESS OFFERING -1 depth, while every demand Vow paid.
        var fragilityMult = 1f;
        foreach (var v in DistinctVows(skills))
            if (v.DamageTakenIncrease > 0f) fragilityMult *= 1f + v.DamageTakenIncrease;

        // TARGETING is "first alive in spawn order", deliberately, and overkill is discarded.
        //
        // Focusing the weakest would let a single-target build tidy up a Swarm efficiently, which is
        // exactly the pressure Swarm exists to apply. Spawn order is also predictable, which matters
        // when the player cannot watch and react — they must be able to reason about a composition
        // before they descend.
        var alive = creatures.Count;
        var since = champ.ElapsedMs;
        var healthAtStart = champ.Health;
        if (metrics is not null) metrics.CreaturesPresent = creatures.Count;
        var nextAuto = AutoAttackIntervalMs;
        var nextBite = enemyIntervalMs;

        (WaveOutcome, List<BattleEvent>) Finish(WaveOutcome o, int atMs)
        {
            champ.ElapsedMs += atMs;
            if (metrics is not null)
            {
                metrics.DurationMs = atMs;
                metrics.HealthLost = Math.Max(0, healthAtStart - champ.Health);
            }
            return (o, events);
        }

        (WaveOutcome, List<BattleEvent>) Kill(int atMs)
        {
            // No EnemyDown here — LandOn emits one per creature as it falls, so this would double the
            // last one and the screen would remove a sprite that was already gone.

            // SECOND WIND — healed on the CLEAR, so it is a reward for finishing rather than a trickle.
            if (shape.HealOnClear > 0f)
                Heal((int)MathF.Round(champ.MaxHealth * shape.HealOnClear), atMs);

            if (harvestChance > 0f && rng.NextDouble() < harvestChance) bonus?.AddCores(1);
            if (triggers.Contains(BuildTrigger.Splinter)) bonus?.AddQuality(0.15f);   // richer loot on a kill (see the blurb)

            return Finish(WaveOutcome.Cleared, atMs);
        }

        // How much a hit is worth right now: build mods, AFFINITY, the Source matchup, BLOODLUST, and MARK.
        float Amp(int absMs, Source? skillSource, Form? skillForm = null, WaveCreature? against = null)
        {
            var m = mods.Damage;

            // AFFINITY — the Nen hexagon. A skill in your affinity's Form hits far harder than one in its
            // opposite. Auto-attacks pass no Form and are unaffected.
            if (build.Affinity is { } aff && skillForm is { } f)
                m *= FormBehaviour.AffinityFactor(aff, f);

            if (against?.Source is { } target && skillSource is { } s)
                m *= Weaving.SourceEffectiveness(s, target, wt);

            // BLOODLUST — damage scales with health MISSING. The keystone that rewards the edge.
            if (triggers.Contains(BuildTrigger.Bloodlust))
            {
                var missing = 1f - champ.Health / (float)Math.Max(1, champ.MaxHealth);
                m *= 1f + 0.8f * missing;
            }

            // ZEAL — the mirror: damage scales with health PRESENT. JUGGERNAUT's reward for staying whole,
            // and the natural partner of the UNBROKEN vow. It denies healing (the keystone grants NoHealing
            // too), so the bonus fades as you are chipped and can never be bought back — a front-loaded
            // fortress, not a sustain build.
            if (triggers.Contains(BuildTrigger.Zeal))
            {
                var present = champ.Health / (float)Math.Max(1, champ.MaxHealth);
                m *= 1f + 0.8f * present;
            }

            if (champ.MarkUntilMs > absMs) m *= FormBehaviour.MarkMultiplier + shape.MarkPowerBonus;

            // ── THE SKILL TREE. Everything past here is gated on skillForm, so the background
            //    auto-attack never triggers a node — it is a trickle, not a build. ────────────────────
            if (skillForm is null) return m;

            m *= shape.HitSize * shape.DamageDealt;

            // The character's APTITUDE. Behind the same skillForm gate as everything else here, so it
            // lifts woven skills and never the background auto-attack.
            m *= shape.FormPowerFor(skillForm.Value);

            // HOARDER — the AVARICE terminal. Every point of haul the path bought becomes force, so a
            // Greed/Fortune build finally has a reason to exist in a fight instead of only in the bag.
            // A fifth of the haul multiplier, so a doubled haul is +20% hit size — enough to matter,
            // far short of making the economy path the strongest damage road in the game.
            if (triggers.Contains(BuildTrigger.Hoarder))
                m *= 1f + HoarderHaulToForce * MathF.Max(0f, mods.Haul - 1f);
            if (shape.HitSizePerMaxHealth > 0f) m *= 1f + shape.HitSizePerMaxHealth * champ.MaxHealth;   // ANCHOR
            if (shape.DamagePerMaxHealth > 0f) m *= 1f + shape.DamagePerMaxHealth * champ.MaxHealth;     // BASTION

            if (against is not null)
            {
                // FOLLOW THROUGH / OPENER / ALPHA — the first hit a creature takes, and the price ALPHA
                // charges on every later one.
                m *= struckOnce.Contains(against) ? shape.LaterHitMultiplier : shape.FirstHitMultiplier;

                // CULL — finishing damage. Reads the creature in front, so in a multi-creature wave it
                // fires on whichever is hurt rather than on the wave as a whole.
                if (shape.CullBonus > 0f && against.Health < against.MaxHealth * shape.CullThreshold)
                    m *= 1f + shape.CullBonus;

                // SIEGE — the branch's one explicitly narrow node.
                if (shape.VsArmouredBonus > 0f || shape.VsOtherPenalty > 0f)
                    m *= against.Archetype == Encounters.Archetype.Armoured
                        ? 1f + shape.VsArmouredBonus
                        : 1f - shape.VsOtherPenalty;
            }

            // SWARMBANE — the more of them there are, the harder you hit. The mirror of what a Swarm
            // band does to a single-target build.
            if (shape.PerCreatureBonus > 0f) m *= 1f + shape.PerCreatureBonus * alive;

            // FIRST STRIKE — the opening seconds are everything, and everything after is nothing.
            if (shape.OpeningSeconds > 0f)
                m *= absMs - since < shape.OpeningSeconds * 1000f
                    ? 1f + shape.OpeningBonus
                    : 1f - shape.AfterOpeningPenalty;

            // INTERRUPT. There is no windup model; the enemy bites on an interval, so the last quarter of
            // that interval IS the windup, and damage landed there is what interrupting means here.
            if (shape.InterruptBonus > 0f
                && absMs - since > 0
                && (absMs - since) % enemyIntervalMs >= enemyIntervalMs * 3 / 4)
                m *= 1f + shape.InterruptBonus;

            return m;
        }

        /// <summary>The first living creature in spawn order, or null when the wave is clear.</summary>
        WaveCreature? FirstAlive()
        {
            for (var i = 0; i < creatures.Count; i++)
                if (creatures[i].Alive) return creatures[i];
            return null;
        }

        int IndexOf(WaveCreature c)
        {
            for (var i = 0; i < creatures.Count; i++)
                if (ReferenceEquals(creatures[i], c)) return i;
            return 0;
        }

        void LandOn(WaveCreature? target, float dmg, int atMs, bool fromSkill = false, bool ignoresArmour = false)
        {
            if (target is null || !target.Alive) return;

            // CRIT lands on SKILL hits only — the idle auto-swing and the poison bleed never crit (both
            // call this with fromSkill:false). Applied first, so a crit stings with more poison too.
            if (fromSkill) dmg *= critFactor;

            // VENOM poisons on SKILL hits only (the blurb says "SKILLS POISON") — never on the auto-attack,
            // and never on the poison's own bleed, or it would feed itself. Fed from the hit's RAW force,
            // before armour: poison is a fraction of how hard you swung, not of how much got through,
            // otherwise armour would shrink the pool AND the bleed and "poison answers armour" would be
            // false twice over.
            if (fromSkill && venomFrac > 0f) poison += dmg * venomFrac;

            // ENEMY ARMOUR is flat and per-hit (see MinHitFraction), so it reads hit SIZE. Poison bypasses
            // it entirely — a bleed tick is small by construction and flat armour would erase it, which
            // would leave the Venom path with nothing to be good at.
            if (metrics is not null) metrics.RawDamage += dmg;

            var raw = dmg;

            // OVERWHELM. Both halves of the mastery: a hit under the floor lands for NOTHING, and a hit
            // over it ignores armour entirely. It is the one node in the tree that can make a build deal
            // literally zero, which is exactly why it is priced at a mastery and why it makes every
            // Spread build unplayable.
            if (fromSkill && shape.OverwhelmFloor > 0f)
            {
                if (dmg < shape.OverwhelmFloor)
                {
                    if (metrics is not null) metrics.Hits++;
                    return;
                }
                ignoresArmour = true;
            }

            if (!ignoresArmour && target.Defense > 0f)
            {
                var armour = target.Defense;

                // SHARPENED / EXECUTIONER cut flat armour; CRUSH halves whatever is left, but only for a
                // hit already many times the creature's mitigation — it rewards size, not persistence.
                if (fromSkill)
                {
                    armour = MathF.Max(0f, armour - shape.ArmourPenetration);
                    if (shape.CrushArmourMultiple > 0f && armour > 0f
                        && dmg > armour * shape.CrushArmourMultiple)
                        armour *= 1f - shape.ArmourIgnoreFraction;
                }

                if (armour > 0f) dmg = MathF.Max(dmg * MinHitFraction, dmg - armour);
            }

            if (metrics is not null)
            {
                metrics.DeliveredDamage += dmg;
                metrics.Hits++;
            }

            // Overkill is DISCARDED rather than carried to the next creature. A 110 Trap hit into a
            // 30-health swarm creature wastes 80, and that waste is the whole cost of bringing a
            // large-hit build to a Swarm band.
            target.Health -= dmg;
            if (fromSkill) struckOnce.Add(target);

            // SUNDER — armour stripped for the rest of the wave. Reads the RAW force of the swing, not
            // what got through: a hit that armour mostly absorbed still bent the plate.
            if (fromSkill && shape.SunderThreshold > 0f && raw >= shape.SunderThreshold)
                target.Defense = MathF.Max(0f, target.Defense - shape.SunderAmount);

            if (metrics is not null && target.Health <= 0f) metrics.CreaturesKilled++;
            var idx = IndexOf(target);
            events.Add(new BattleEvent(BattleEventKind.Strike, idx, (int)MathF.Round(dmg), atMs));
            if (!target.Alive)
            {
                alive--;
                if (shape.CascadeOnKill) cascadeArmed = true;

                // One EnemyDown per CREATURE, not per wave. The screen needs to know which sprite to
                // remove; the wave-cleared signal is the outcome, not this event.
                events.Add(new BattleEvent(BattleEventKind.EnemyDown, idx, 0, atMs));

                // BREAKER — half of the overkill carries on. The design named a part-break bonus here;
                // the sim has no part-break model, and this answers the same complaint from inside the
                // model that exists, because discarded overkill IS the tax a large-hit build pays in a
                // Swarm band. One level only: a chain of carries would let one hit clear a whole wave.
                var spill = -target.Health;
                if (fromSkill && shape.OverkillCarry > 0f && spill > 0f && FirstAlive() is { } next)
                    LandOn(next, spill * shape.OverkillCarry, atMs, fromSkill: false, ignoresArmour: true);
            }
        }

        /// <summary>
        /// Land one activation across up to <paramref name="targets"/> living creatures. Returns the raw
        /// total dealt, which is what leech reads.
        /// </summary>
        float LandSpread(float raw, int atMs, int targets, Source? skillSource, Form? skillForm, int absMs,
                         bool fromSkill = true)
        {
            if (targets <= 0) return 0f;
            if (fromSkill && metrics is not null) metrics.Activations++;

            // CASCADE — one activation after a kill reaches everything. Armed in LandOn, spent here, so
            // the reward lands on the NEXT cast and a player can see the ripple rather than guess at it.
            if (fromSkill && cascadeArmed) { targets = int.MaxValue; cascadeArmed = false; }

            var dealt = 0f;
            var struck = 0;
            var lastIndex = -1;
            for (var i = 0; i < creatures.Count && struck < targets; i++)
            {
                var c = creatures[i];
                if (!c.Alive) continue;
                // Amp is per TARGET: the Source matchup belongs to the creature being hit, so one cast
                // can be strong against one creature in a wave and weak against another.
                var hit = raw * Amp(absMs, skillSource, skillForm, c);
                LandOn(c, hit, atMs, fromSkill);
                dealt += hit;
                struck++;
                lastIndex = i;
                if (fromSkill && metrics is not null) metrics.TargetsStruck++;
            }

            if (!fromSkill) return dealt;

            // CHAIN and RICOCHET both reach PAST the activation's own target count, which is what makes
            // them Spread nodes rather than damage nodes — they buy action economy, and a build already
            // striking everything gains nothing from either.
            var extraFraction = shape.ChainFraction;
            if (shape.RicochetChance > 0f && rng.NextDouble() < shape.RicochetChance)
                extraFraction = MathF.Max(extraFraction, shape.RicochetFraction);

            if (extraFraction > 0f)
                for (var i = lastIndex + 1; i < creatures.Count; i++)
                {
                    var c = creatures[i];
                    if (!c.Alive) continue;
                    var hit = raw * extraFraction * Amp(absMs, skillSource, skillForm, c);
                    LandOn(c, hit, atMs, fromSkill: true);
                    dealt += hit;
                    struck++;
                    if (metrics is not null) metrics.TargetsStruck++;
                    break;
                }

            // FEEDBACK — the Spread/Endure bridge. Sustain that scales with how WIDE you are, which is
            // the only way the two branches have of paying each other.
            if (shape.HealPerTargetStruck > 0f && struck > 0)
                Heal((int)MathF.Round(champ.MaxHealth * shape.HealPerTargetStruck * struck), atMs);

            // LEECH. Reads the total dealt across every creature touched, so a wide build heals wider.
            if (shape.Leech > 0f && dealt > 0f)
                Heal((int)MathF.Round(dealt * shape.Leech), atMs);

            return dealt;
        }

        void Heal(int amount, int atMs)
        {
            // BLOOD MAGIC. The cost is total: it does not reduce healing, it removes it.
            if (triggers.Contains(BuildTrigger.NoHealing)) return;

            // THE BAND'S SUSTAIN MULTIPLIER, applied at the one funnel every heal already passes through
            // — leech, HEAL PER TARGET, SECOND WIND's on-clear heal, all four call sites. Bands.Endless
            // ("leech and regeneration halved") computed its 0.5 and had NO CALLER anywhere in the
            // solution, so the affix that exists to pressure ENDURE did nothing to it whatever.
            // Deliberately NOT applied to FullHealBetweenWaves: that is a reset, not regeneration, and
            // halving a binary is a design change rather than a repair.
            amount = (int)MathF.Round(amount * sustain);
            if (amount <= 0) return;
            champ.Health = Math.Min(champ.MaxHealth, champ.Health + amount);
            events.Add(new BattleEvent(BattleEventKind.Heal, 0, amount, atMs));
        }

        for (var ms = tuning.TickMs; ms <= tuning.TickCeilingMs; ms += tuning.TickMs)
        {
            var abs = since + ms;

            // ── VENOM bleeds first, so poison from earlier ticks can finish an enemy before it swings. ──
            if (poison > 0.5f && ms % 500 == 0)
            {
                var bite = poison * VenomBleedPerHalfSecond;
                poison -= bite;
                // Poison bleeds into the front of the wave. It is a single pool, not per creature — a
                // build that poisons then watches its target die keeps the standing damage.
                LandOn(FirstAlive(), bite, ms, ignoresArmour: true);   // not fromSkill — must not re-poison
                if (alive == 0) return Kill(ms);
            }

            // ── SKILLS ────────────────────────────────────────────────────────────────────────
            for (var i = 0; i < skills.Count; i++)
            {
                var sk = skills[i];
                var form = sk.Form;

                // TRAP fires only when bitten; it is handled at the enemy's swing, not here.
                if (FormBehaviour.FiresOnBeingHit(form)) continue;

                if (FormBehaviour.IsPassive(form))
                {
                    // AURA: always on, no cooldown. Ticks steadily for the whole fight. RADIANCE (an item
                    // enchantment) ticks it faster — same damage per tick, so more ticks is more DPS, and
                    // that item is worth nothing to a build that isn't running Aura.
                    var auraTick = triggers.Contains(BuildTrigger.Radiance) ? FormBehaviour.AuraTickMs * 3 / 5 : FormBehaviour.AuraTickMs;
                    if (ms % auraTick != 0) continue;
                    var aura = FormBehaviour.BaseDamage(form, resonance, wt)
                               * VowFactor(sk, weaveCtx, wt)
                               * (FormBehaviour.AuraTickMs / 1000f);
                    LandSpread(aura, ms, shape.TargetsFor(form), sk.Source, form, abs);
                    if (alive == 0) return Kill(ms);
                    continue;
                }

                var cd = Math.Max(1, (int)(FormBehaviour.BaseCooldownMs(form)
                                           / Math.Max(0.1f, mods.SkillRate * shape.SkillRate)));

                // PREPARATION — every skill's FIRST cast of a wave is free of its cooldown. The default
                // ReadyAt of `cd` is what normally makes a skill wait one cooldown before its opener;
                // dropping that to 0 is the whole node, and it is worth most to a slow, heavy build.
                var opening = shape.FreeOpeningCast ? 0 : cd;
                if (abs < champ.ReadyAt.GetValueOrDefault(i, opening)) continue;
                champ.ReadyAt[i] = abs + cd;

                if (FormBehaviour.IsAmplifier(form))
                {
                    // MARK deals nothing. It opens a window. LINGER (an item enchantment) stretches that
                    // window, so a Mark build gets far more of its big hits inside the amplify.
                    var window = triggers.Contains(BuildTrigger.Linger) ? FormBehaviour.MarkWindowMs * 9 / 5 : FormBehaviour.MarkWindowMs;
                    window = (int)(window * shape.MarkWindowMultiplier);   // MARK MASTERY
                    champ.MarkUntilMs = abs + window;
                    events.Add(new BattleEvent(BattleEventKind.Skill, 0, (int)Form.Mark, ms));
                    continue;
                }

                // ECHO fires the whole thing twice; OVERDRAW adds a cast to PROJECTILE only. Both are
                // build commitments — Echo is a keystone you socketed, Overdraw is an item that is dead
                // unless this skill is a Projectile.
                var casts = triggers.Contains(BuildTrigger.Echo) ? 2 : 1;
                if (form == Form.Projectile && triggers.Contains(BuildTrigger.Overdraw)) casts += 1;
                for (var c = 0; c < casts; c++)
                {
                    var raw = FormBehaviour.BaseDamage(form, resonance, wt)
                              * VowFactor(sk, weaveCtx, wt);

                    // EXECUTE — a STRIKE finishes a weakened enemy. Reads the CURRENT target's own health
                    // fraction, so in a multi-creature wave it fires on whichever creature is in front and
                    // hurt, not on the wave as a whole. Dead without a Strike, and strongest on bosses —
                    // trash is dead before it reaches the threshold.
                    if (form == Form.Strike && triggers.Contains(BuildTrigger.Execute)
                        && FirstAlive() is { } victim
                        && victim.Health < victim.MaxHealth * ExecuteThreshold)
                        raw *= ExecuteMultiplier;

                    // ASSASSINATE — once a wave, a weakened creature simply dies. Deliberately not a
                    // damage bonus: against a Bruiser with a large pool, "kill it outright" and "hit it
                    // hard" are different promises, and only the first one answers a Caster band.
                    if (!assassinated && shape.AssassinateThreshold > 0f
                        && FirstAlive() is { } mark
                        && mark.Health < mark.MaxHealth * shape.AssassinateThreshold)
                    {
                        assassinated = true;
                        LandOn(mark, mark.Health, ms, fromSkill: false, ignoresArmour: true);
                        if (alive == 0) return Kill(ms);
                    }

                    events.Add(new BattleEvent(BattleEventKind.Skill, 0, (int)form, ms));
                    var dealt = LandSpread(raw, ms, shape.TargetsFor(form), sk.Source, form, abs);

                    // WEAVER — the ARTIFICE terminal. The cast ALSO lands as the next Form in the loadout,
                    // with that Form's own base damage, target count and Source matchup. One slot answers
                    // two of the content's four demands, which nothing else in the game does: a Strike
                    // followed by an Aura is a Weight hit and a Spread hit from one activation.
                    //
                    // Skipped for Mark (which deals nothing and only opens a window) and for Trap (which
                    // fires on being bitten, not on a cooldown) — weaving into either would be a silent
                    // no-op or a second Trap that ignored its own rule.
                    if (triggers.Contains(BuildTrigger.Weaver) && skills.Count > 1)
                    {
                        var woven = skills[(i + 1) % skills.Count];
                        if (!FormBehaviour.IsAmplifier(woven.Form) && !FormBehaviour.FiresOnBeingHit(woven.Form))
                        {
                            var wovenRaw = FormBehaviour.BaseDamage(woven.Form, resonance, wt)
                                           * VowFactor(woven, weaveCtx, wt)
                                           * WeaverEchoFraction;
                            events.Add(new BattleEvent(BattleEventKind.Skill, 0, (int)woven.Form, ms));
                            dealt += LandSpread(wovenRaw, ms, shape.TargetsFor(woven.Form),
                                                woven.Source, woven.Form, abs);
                            if (alive == 0) return Kill(ms);
                        }
                    }

                    if (FormBehaviour.Heals(form))
                    {
                        // SIPHON deepens TRANSFORMATION's leech. Dead without Transformation — nothing else
                        // heals on hit, so the enchant is inert on any other build. Leeches from the TOTAL
                        // dealt, so a multi-target Transformation heals from every creature it touches.
                        var leech = FormBehaviour.TransformationLeech;
                        if (triggers.Contains(BuildTrigger.Siphon)) leech *= SiphonLeechMultiplier;
                        Heal((int)MathF.Round(dealt * leech), ms);
                    }

                    if (alive == 0) return Kill(ms);
                }
            }

            // ── AUTO-ATTACK. Small on purpose — see AutoAttackDamage. ─────────────────────────
            if (ms >= nextAuto)
            {
                nextAuto += AutoAttackIntervalMs;
                LandSpread(AutoAttackDamage, ms, 1, null, null, abs, fromSkill: false);
                if (alive == 0) return Kill(ms);
            }

            // ── THE ENEMY BITES BACK ──────────────────────────────────────────────────────────
            //
            // ACCUMULATED, never `ms % enemyIntervalMs`, which is what it used to be and which made the
            // SWIFT affix do the opposite of its name. The loop steps in TickMs (100ms), so an exact
            // modulo only fires where the interval divides a multiple of the tick — the real gap between
            // bites is lcm(tick, interval), not the interval. SWIFT multiplies the interval by 0.7:
            //
            //   attack bias   plain      SWIFT wanted    SWIFT actually got
            //   Normal 1500   1500 ms    1050 ms         2100 ms   (40% FEWER bites)
            //   Heavy  2200   2200 ms    1540 ms         7700 ms   (5x fewer)
            //   Fast   1000   1000 ms     700 ms          700 ms   (correct, by luck of the divisor)
            //
            // "The creatures strike more often" was a band the player could learn to fear, and on two of
            // the three attack biases it was a band that let them rest. The auto-attack ten lines above
            // has always accumulated its own next time; this now does the same.
            if (ms >= nextBite)
            {
                nextBite += enemyIntervalMs;
                // EVERY LIVING CREATURE BITES. This is what makes action economy real: a Swarm's combined
                // damage is its threat, and every creature killed is incoming damage removed. A build that
                // cannot clear a Swarm quickly does not merely kill slowly, it takes the full wave's
                // damage for the whole fight.
                var incoming = 0f;
                for (var ci = 0; ci < creatures.Count; ci++)
                    if (creatures[ci].Alive) incoming += creatures[ci].Damage;

                var taken = incoming / Math.Max(0.05f, mods.Health) * defenseFactor * fragilityMult;

                // ── ENDURE. Applied in this order on purpose: multipliers first, then the flat cut, so
                //    PADDING is worth MORE to a build that already mitigates — small bites are what a
                //    flat reduction erases, and that is the branch's whole answer to a Swarm. ────────
                taken *= shape.DamageTaken;

                // ABSORB — mitigation rises as health falls, to its cap at death's door. The node that
                // makes a low-health build survivable without making a healthy one invincible.
                if (shape.AbsorbAtLowHealth > 0f)
                {
                    var missing = 1f - champ.Health / (float)Math.Max(1, champ.MaxHealth);
                    taken *= 1f - shape.AbsorbAtLowHealth * missing;
                }

                taken = MathF.Max(0f, taken - shape.FlatDamageReduction);

                // FORTIFY — the first bite of each wave deals nothing. Worth most where bites are large
                // and rare, which is precisely a Bruiser band.
                if (shape.FirstBiteFree && !firstBiteTaken) taken = 0f;
                firstBiteTaken = true;

                champ.Health -= (int)MathF.Round(taken);
                events.Add(new BattleEvent(BattleEventKind.EnemyStrike, 0, (int)MathF.Round(taken), ms));

                // TRAP: the only Form that pays for being hit. This is why a build takes it.
                for (var idx = 0; idx < skills.Count; idx++)
                {
                    var sk = skills[idx];
                    if (!FormBehaviour.FiresOnBeingHit(sk.Form)) continue;

                    // COILED re-arms the TRAP far faster, so it answers more bites. This loop only runs for
                    // a woven Trap, so the enchant is naturally dead on any build without one.
                    var trapBase = FormBehaviour.BaseCooldownMs(Form.Trap);
                    if (triggers.Contains(BuildTrigger.Coiled)) trapBase = (int)(trapBase * CoiledCooldownFactor);
                    var cd = Math.Max(1, (int)(trapBase / Math.Max(0.1f, mods.SkillRate * shape.SkillRate)));
                    if (abs < champ.ReadyAt.GetValueOrDefault(idx, 0)) continue;
                    champ.ReadyAt[idx] = abs + cd;

                    var trapRaw = FormBehaviour.BaseDamage(Form.Trap, resonance, wt)
                                  * VowFactor(sk, weaveCtx, wt);
                    events.Add(new BattleEvent(BattleEventKind.Skill, 0, (int)Form.Trap, ms));
                    LandSpread(trapRaw, ms, shape.TargetsFor(Form.Trap), sk.Source, Form.Trap, abs);
                    if (alive == 0) return Kill(ms);
                }

                if (!champ.Alive)
                {
                    if (!champ.UndyingSpent && triggers.Contains(BuildTrigger.Undying))
                    {
                        champ.UndyingSpent = true;
                        champ.Health = 1;
                        events.Add(new BattleEvent(BattleEventKind.Shield, 0, 0, ms));
                    }
                    else
                    {
                        champ.Health = 0;
                        events.Add(new BattleEvent(BattleEventKind.Down, 0, 0, ms));
                        return Finish(WaveOutcome.Wiped, ms);
                    }
                }
            }
        }

        // Could not kill it inside the ceiling: a stall, not a hang. Paid out as a retreat.
        return Finish(WaveOutcome.Stalled, tuning.TickCeilingMs);
    }

    /// <summary>The floor under a champion's pool, so an untrained Hunter still has a run to lose.</summary>
    private const int MinimumChampionHealth = 60;

    /// <summary>
    /// A StaticCost Vow's HEALTH price, as a multiplier on the champion's max health at mint.
    /// </summary>
    /// <remarks>
    /// RECKLESS OFFERING pays for its power this way — a smaller pool for the whole run. FRAGILITY, the
    /// other StaticCost Vow, pays as damage taken instead (see the fragility multiplier in ResolveWave), so
    /// it is excluded here — charging its effective-HP as health as well would bill the same cost twice.
    /// Applied ONCE at mint (the champion persists across waves), which is why it lives here as a helper the
    /// mint sites call rather than inside the per-wave sim. The product across every such Vow the build wears.
    /// </remarks>
    /// <summary>The champion's starting health for a run: the Hunter's pool, as the BUILD changes it.</summary>
    /// <remarks>
    /// <para>
    /// EVERY mint site calls this. It exists because there were three of them — the live screen, the
    /// bench, and a balance harness — each open-coding <c>Math.Max(60, hunter.MaxHealth) * ...</c>, and
    /// the third had quietly dropped the <see cref="VowHealthMultiplier"/> term. Nothing shipped was
    /// wrong; the MEASUREMENTS were, which is worse in its way, because a measurement that lies is
    /// trusted. RECKLESS OFFERING looked like the best Vow in the game for a while on the strength of a
    /// harness that never charged its health price, and a build's health looked worthless because a
    /// third of the multiplier chain was missing from the only place anybody was reading numbers off.
    /// </para>
    /// <para>
    /// The lesson is the one this codebase keeps relearning: a formula copied is a formula that will
    /// drift, and the copy that drifts is never the one you are looking at.
    /// </para>
    /// </remarks>
    public static int ChampionHealth(Build build, Economy.Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);
        // VowHealthMultiplier already folds in the build's own MaxHealth term; do not apply it twice.
        var pool = Math.Max(MinimumChampionHealth, hunter.MaxHealth) * VowHealthMultiplier(build);
        return Math.Max(1, (int)MathF.Round(pool));
    }

    public static float VowHealthMultiplier(Build build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var mult = 1f;
        foreach (var v in DistinctVows(build.Skills))
            if (v is { Kind: VowKind.StaticCost, StaticCostMagnitude: > 0f, DamageTakenIncrease: 0f })
                mult *= 1f - v.StaticCostMagnitude;

        // TOUGHNESS raises the pool and ENDLESS halves it, and both belong here rather than in the wave
        // sim for the same reason a Vow's health price does: the champion persists across a whole descent,
        // so a per-wave multiplier would compound the change once for every wave walked.
        mult *= build.Shape.MaxHealth;

        return MathF.Max(0.05f, mult);
    }

    /// <summary>Every Vow the build has sworn, counted once however many skills carry it.</summary>
    /// <remarks>
    /// The single place that decides what "wearing a Vow twice" means, so the two price sites cannot
    /// answer it differently. It means nothing: a Vow is a promise about the build, and a promise made
    /// on four skills is one promise. Keyed on Id rather than the record, because two catalogue entries
    /// could in principle be value-equal and still be different Vows.
    /// </remarks>
    private static IEnumerable<Vow> DistinctVows(IEnumerable<EquippedSkill> skills)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sk in skills)
            if (sk.Vow is { } v && seen.Add(v.Id))
                yield return v;
    }

    /// <summary>A skill's Vow multiplier, if the BUILD meets its demand.</summary>
    /// <remarks>
    /// The context is a property of the build, not of the moment, so it is built once per wave and every
    /// skill is judged against the same one. A Vow that read the fight — below 40% health, against a
    /// boss — was a lottery on how the wave went in a game where the player cannot react; this is a
    /// decision they made at the workbench and can see the consequences of in the report.
    /// </remarks>
    private static float VowFactor(EquippedSkill sk, WeaveContext ctx, WeavingTuning wt)
    {
        if (sk.Vow is not { } vow) return 1f;
        return Weaving.IsActive(vow, ctx) ? Weaving.VowMultiplier(vow, wt) : 1f;
    }

    /// <summary>Describe a build to the Vow layer.</summary>
    public static WeaveContext DescribeBuild(Build build, Economy.Hunter hunter)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(hunter);

        var worn = new HashSet<BareSlot>();
        foreach (var (slot, bare) in new[]
                 {
                     (Economy.GearSlot.Boots, BareSlot.Boots),
                     (Economy.GearSlot.Gloves, BareSlot.Gloves),
                     (Economy.GearSlot.Helm, BareSlot.Helm),
                     (Economy.GearSlot.Ring, BareSlot.Ring),
                     (Economy.GearSlot.Charm, BareSlot.Charm),
                 })
            if (hunter.Worn(slot) is not null) worn.Add(bare);

        var mods = build.Resolve(hunter);
        return new WeaveContext(
            DistinctForms: build.Skills.Select(s => s.Form).Distinct().Count(),
            DistinctSources: build.Skills.Select(s => s.Source).Distinct().Count(),
            SkillsWoven: build.Skills.Count,
            // The BUILD's capacity, not the type's floor. VOW OF COMPLETION demands "no skill slot is
            // empty", and against a hard 4 a player who had bought the fifth weave met it with four
            // skills and an empty fifth slot in front of them.
            SkillSlots: build.SlotCapacity,
            CritPercent: hunter.ValueOf(Economy.HunterStat.CriticalChance)
                         + hunter.AffixTotal(Economy.AffixStat.Crit)
                         + build.Shape.BonusCritPercent,
            BaseCritPercent: Economy.ProgressionTuning.Default.BaseValue[Economy.HunterStat.CriticalChance],
            SkillRate: mods.SkillRate * build.Shape.SkillRate,
            Defence: hunter.Defense,
            KeystonesWorn: build.Keystones.Count,
            WornSlots: worn);
    }
}
