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

    /// <summary>FLAT per-hit mitigation — see <see cref="SoloBattle.MinHitFraction"/>.</summary>
    public float Defense { get; init; }

    public Source? Source { get; init; }

    public bool Alive => Health > 0f;

    public static WaveCreature Single(float health, float damage, float defense = 0f, Source? source = null)
        => new() { MaxHealth = health, Health = health, Damage = damage, Defense = defense, Source = source };
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
        bool isBoss = false)
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
            (hunter.ValueOf(Economy.HunterStat.CriticalChance) + hunter.AffixTotal(Economy.AffixStat.Crit)) / 100f,
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
        var fragilityMult = 1f;
        foreach (var sk in skills)
            if (sk.Vow is { DamageTakenIncrease: > 0f } v) fragilityMult *= 1f + v.DamageTakenIncrease;

        // TARGETING is "first alive in spawn order", deliberately, and overkill is discarded.
        //
        // Focusing the weakest would let a single-target build tidy up a Swarm efficiently, which is
        // exactly the pressure Swarm exists to apply. Spawn order is also predictable, which matters
        // when the player cannot watch and react — they must be able to reason about a composition
        // before they descend.
        var alive = creatures.Count;
        var since = champ.ElapsedMs;
        var nextAuto = AutoAttackIntervalMs;

        (WaveOutcome, List<BattleEvent>) Finish(WaveOutcome o, int atMs)
        {
            champ.ElapsedMs += atMs;
            return (o, events);
        }

        (WaveOutcome, List<BattleEvent>) Kill(int atMs)
        {
            // No EnemyDown here — LandOn emits one per creature as it falls, so this would double the
            // last one and the screen would remove a sprite that was already gone.

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

            if (champ.MarkUntilMs > absMs) m *= FormBehaviour.MarkMultiplier;
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
            if (!ignoresArmour && target.Defense > 0f)
                dmg = MathF.Max(dmg * MinHitFraction, dmg - target.Defense);

            // Overkill is DISCARDED rather than carried to the next creature. A 110 Trap hit into a
            // 30-health swarm creature wastes 80, and that waste is the whole cost of bringing a
            // large-hit build to a Swarm band.
            target.Health -= dmg;
            var idx = IndexOf(target);
            events.Add(new BattleEvent(BattleEventKind.Strike, idx, (int)MathF.Round(dmg), atMs));
            if (!target.Alive)
            {
                alive--;
                // One EnemyDown per CREATURE, not per wave. The screen needs to know which sprite to
                // remove; the wave-cleared signal is the outcome, not this event.
                events.Add(new BattleEvent(BattleEventKind.EnemyDown, idx, 0, atMs));
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
            var dealt = 0f;
            var struck = 0;
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
            }
            return dealt;
        }

        void Heal(int amount, int atMs)
        {
            // BLOOD MAGIC. The cost is total: it does not reduce healing, it removes it.
            if (triggers.Contains(BuildTrigger.NoHealing)) return;
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
                               * VowFactor(sk, champ, abs, isBoss, wt)
                               * (FormBehaviour.AuraTickMs / 1000f);
                    LandSpread(aura, ms, FormBehaviour.Targets(form), sk.Source, form, abs);
                    if (alive == 0) return Kill(ms);
                    continue;
                }

                var cd = Math.Max(1, (int)(FormBehaviour.BaseCooldownMs(form) / Math.Max(0.1f, mods.SkillRate)));
                if (abs < champ.ReadyAt.GetValueOrDefault(i, cd)) continue;
                champ.ReadyAt[i] = abs + cd;

                if (FormBehaviour.IsAmplifier(form))
                {
                    // MARK deals nothing. It opens a window. LINGER (an item enchantment) stretches that
                    // window, so a Mark build gets far more of its big hits inside the amplify.
                    var window = triggers.Contains(BuildTrigger.Linger) ? FormBehaviour.MarkWindowMs * 9 / 5 : FormBehaviour.MarkWindowMs;
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
                              * VowFactor(sk, champ, abs, isBoss, wt);

                    // EXECUTE — a STRIKE finishes a weakened enemy. Reads the CURRENT target's own health
                    // fraction, so in a multi-creature wave it fires on whichever creature is in front and
                    // hurt, not on the wave as a whole. Dead without a Strike, and strongest on bosses —
                    // trash is dead before it reaches the threshold.
                    if (form == Form.Strike && triggers.Contains(BuildTrigger.Execute)
                        && FirstAlive() is { } victim
                        && victim.Health < victim.MaxHealth * ExecuteThreshold)
                        raw *= ExecuteMultiplier;

                    events.Add(new BattleEvent(BattleEventKind.Skill, 0, (int)form, ms));
                    var dealt = LandSpread(raw, ms, FormBehaviour.Targets(form), sk.Source, form, abs);

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
            if (ms % enemyIntervalMs == 0)
            {
                // EVERY LIVING CREATURE BITES. This is what makes action economy real: a Swarm's combined
                // damage is its threat, and every creature killed is incoming damage removed. A build that
                // cannot clear a Swarm quickly does not merely kill slowly, it takes the full wave's
                // damage for the whole fight.
                var incoming = 0f;
                for (var ci = 0; ci < creatures.Count; ci++)
                    if (creatures[ci].Alive) incoming += creatures[ci].Damage;

                var taken = incoming / Math.Max(0.05f, mods.Health) * defenseFactor * fragilityMult;
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
                    var cd = Math.Max(1, (int)(trapBase / Math.Max(0.1f, mods.SkillRate)));
                    if (abs < champ.ReadyAt.GetValueOrDefault(idx, 0)) continue;
                    champ.ReadyAt[idx] = abs + cd;

                    var trapRaw = FormBehaviour.BaseDamage(Form.Trap, resonance, wt)
                                  * VowFactor(sk, champ, abs, isBoss, wt);
                    events.Add(new BattleEvent(BattleEventKind.Skill, 0, (int)Form.Trap, ms));
                    LandSpread(trapRaw, ms, FormBehaviour.Targets(Form.Trap), sk.Source, Form.Trap, abs);
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
    public static float VowHealthMultiplier(Build build)
    {
        ArgumentNullException.ThrowIfNull(build);
        var mult = 1f;
        foreach (var sk in build.Skills)
            if (sk.Vow is { Kind: VowKind.StaticCost, StaticCostMagnitude: > 0f, DamageTakenIncrease: 0f } v)
                mult *= 1f - v.StaticCostMagnitude;
        return MathF.Max(0.05f, mult);
    }

    /// <summary>A skill's Vow multiplier, if its condition holds right now.</summary>
    private static float VowFactor(EquippedSkill sk, Champion champ, int absMs, bool isBoss, WeavingTuning wt)
    {
        if (sk.Vow is not { } vow) return 1f;

        var frac = champ.Health / (float)Math.Max(1, champ.MaxHealth);
        // Slot 0: the character is always "in the front" now — there is no line to stand in.
        var ctx = new WeaveContext(frac, absMs, isBoss, 0);
        return Weaving.IsActive(vow, ctx) ? Weaving.VowMultiplier(vow, wt) : 1f;
    }
}
