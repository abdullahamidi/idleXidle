# Remaining-skill sweep: working manifest (2026-10-03)
Generated from a code inventory of ae20a7fd. Working document for the sweep, not an owner review item.

## hammer_blow — BLOW
- **owner**: shared (any champion; HAMMER style). Historically the Seeker's signature before HARD HANDS (PlayerLoadout.cs:640)
- **kind**: Active / SkillKind.Active, SkillEffect.Damage, Style.Hammer, Beats 6, Targets 1, BasePower 500, ClipKey 'strike', FxKey 'strike' (SkillCatalogue.cs:390-419)
- **gameplay**: Instant, single heavy direct hit on the front enemy on the beat (500 base power x resonance, x DamageMultiplier). FLATTEN: ignoresArmour on the hit (SoloBattle.cs:2830). TOLL x1.4. BREAKTHROUGH: castCarry 0.5 -> if the blow kills, half the overkill lands on the next FirstAlive as HitSource.Carry, armour-ignoring, same ms (1960-1975). TRAIL: the next basic swing ignores defence (one use). FINISH: after the blow lands, the living enemy with least health under 15% (BRINK 25%, TWICE 20%/2 per wave) is executed: LandOn(max(raw, its health)), ignoresArmour, Primary, can crit (2873-2894); CLEAN CUT carries 100% of that overkill. Offensive. What travels: nothing in Core (direct contact). Player decision: one huge hit vs defence (FLATTEN) or a finisher. Moment to perceive: the single heavy contact; for FINISH the second, separate kill of a weakened enemy; for carry the excess jumping to the next enemy; for TRAIL that the following swing is armour-piercing.
- **trigger**: Takes the champion's action on its beat (6-beat cooldown), resolved in the SoloBattle cast block (SoloBattle.cs:2620-2930).
- **targets**: Single target: LandSpread with Targets 1 hits FirstAlive (the front enemy). FINISH's execute can strike a DIFFERENT enemy (the lowest-health living one under the threshold, SoloBattle.cs:2873-2894). BREAKTHROUGH/CLEAN CUT carry overkill into the next FirstAlive (SoloBattle.cs:1960-1975).
- **persistent**: No standing state, except TRAIL arming a one-use flag (trailArmed) spent by the next basic swing (SoloBattle.cs:2925, 3021) and the once-per-wave execute counter.
- **movement**: None in Core. On screen the Seeker plays the plain Strike-Form clip where he stands (no root motion); ADR-011 line 936: 'the plain knife swing where he stands, 350+ px from the creature'.
- **duration**: Instant on the beat. Execute and carry are the same millisecond.
- **stacking**: None. FINISH: ExecutesPerWave 1 (TWICE: 2).
- **events**: At the beat ms: Skill(Slot=i) first (SoloBattle.cs:2633), optional Charge(0) if REND dumps, then Strike(Slot=creature, Amount, Hit=Primary, Crit) for the blow (1775); EnemyDown if it kills; Carry: Strike(Hit=Carry) on next creature same ms; FINISH: an extra Strike(Hit=Primary, possibly Crit) on the executed creature + EnemyDown, same ms. TRAIL emits nothing; the next swing's Strike(Hit=Swing) is simply armour-ignoring. Possibly DeadweightStored/Released and Break/DefenceNow from other build pieces.
- **variations**: FLATTEN (Body): defence ignore -> show armour being bypassed; TOLL x1.4 (bigger hit), BREAKTHROUGH (50% overkill carries to next enemy -> show excess passing on), TRAIL (next swing ignores defence -> mark the armed hand/the next swing). FINISH (Shadow): post-blow execute of the weakest enemy <15% -> second distinct killing beat possibly on another creature; BRINK 25%, TWICE 2 executes/wave at 20%, CLEAN CUT 100% overkill carry from the execute.
- **current_presentation**: Legacy generic path, no recipe (ActionRecipes.BySkill holds only sig_seeker_hard_hands, ActionRecipe.cs:505-508; ByForm only 'projectile'). Clip: CommitPlainClip('strike') aimed so its contact lands on the beat (HuntScreen.cs:6496-6506) -> char_<champ>_strike strip (Seeker: assets/art/Animations/Roster/seeker_strike/char_seeker_strike_strip8_512.png), 8 equal frames, played in place. Skill event (HuntScreen.cs:2345-2385): callout 'HAMMER' (Ember, CalloutFor 1197), PlaySkillVfx -> VfxProfiles.ForSkill -> CastStrike ('cast.strike', creature Center, 0.55, 12 fps, VfxProfiles.cs:169-170) with FxFor -> fx_seeker_strike_strip8_512 (a white radial spark/streak burst, tinted by SourceGlow(source)) on the target creature; sfx_cast 0.42. Strike event (2219-2330): sfx_hit 0.22, damage number graded as skill (sum per creature), usual F2 flash, ImpactWeak puff every other blow. Execute/carry hits look identical to any other skill hit (separate number on another creature); no EXECUTE callout, no carry visual.
- **current_problems**: Champion swings a knife in place ~350 px from the target (ADR-011:936); burst appears on the creature with no contact, no travel. Generic sfx_cast + sfx_hit; no weight to a 6-beat 'enormous blow'. FINISH execute, BREAKTHROUGH/CLEAN CUT carry and TRAIL armed swing are invisible as distinct moments (just extra numbers / nothing). Generic per-character strike strip is shared by any skill with ClipKey 'strike'.
- **asset_quality**: char_seeker_strike strip is the original Strike-Form knife swing (8x512, plain timing, no .timing file); fx_seeker_strike is a generic radial white streak burst (VFX regeneration lessons memory: fx_seeker_strike is the one regenerated strip). No BLOW-specific sound.
- **proposed_archetype**: MELEE CONTACT (HARD HANDS reference: MeleeActionRecipe, own strip, contact on the beat)
- **secondary_archetype**: composition: FINISH adds an execute TARGET REACTION beat on the weakest enemy; BREAKTHROUGH/CLEAN CUT a carry follow-through to the next enemy; TRAIL a brief armed-state on the hunter until the next swing
- **strategy**: HAMMER: one enormous blow every 6 beats; branches choose armour (FLATTEN) vs death/excess (FINISH).
- **risk**: Must stay isolated from HARD HANDS (same Strike Form/'strike' FxKey; ADR-011:814 leak history) - needs its own BySkill recipe and own strip. Execute can target a non-front creature in the same ms as the blow; carry lands same ms; the replay must not pre-kill creatures before the contact frame. 6-beat cadence means rare, so it must read heavier than HARD HANDS without out-shouting it.
- **showcase_needs**: Seeker fixture with RH_SHOT_SWAP=volley_spray:hammer_blow (active slot); seek DevSeekBefore Skill on that slot. Variations via RH_SHOT_VARIATION=hammer_blow:FLATTEN(+TOLL+BREAKTHROUGH or +TRAIL) and hammer_blow:FINISH+BRINK+CLEAN CUT; needs a multi-creature wave (fightmulti-like) with a creature already under 15-25% when the blow lands, a kill with overkill so carry fires, and an armoured creature for FLATTEN. TRAIL needs the following swing in frame.

## snare_repay — REPAY
- **owner**: shared (any champion; SNARE style)
- **kind**: Active / SkillKind.Active, SkillEffect.Damage, Style.Snare, Beats 5, Targets 1, BasePower 0, PaysBackDamageTaken 2.0, ClipKey 'trap', FxKey 'trap' (SkillCatalogue.cs:454-484)
- **gameplay**: On the beat: raw = owed health damage x 2.0 (VENGEANCE 3.5 within 3s; GRUDGE 5.0; RAW NERVE 7.0 within 2s; SCARRED +50% while champion health <40%) then all usual multipliers; lands as one Primary hit on the front enemy. If nothing was banked it still lands a 0 hit. Delayed: the payoff is the sum of earlier bites. BANKED (Machine): instead of damage, GrantShield(raw) on the champion and no hit (2819-2823); LINING 3.0x, STANDING x1.5, CARRIED: every wave starts with shield = 5% max health (code says 0.05 while the card says 10% - text/dial mismatch, SkillCatalogue.cs:482-483 vs SoloBattle.cs:1205-1206). Offensive (damage) or defensive (BANKED). What travels: conceptually the taken damage returning to the attacker. Moment to perceive: the bank filling as he is bitten, and the cash-out on the beat proportional to it.
- **trigger**: Takes the champion's action on its beat (5-beat cooldown). Its size is fed by a bank: every bite's HEALTH damage (not shield-absorbed) is stamped into takenSinceCast[slot] (SoloBattle.cs:3243-3252); the cast spends and clears it (2649-2667).
- **targets**: Single target, FirstAlive via LandSpread. BANKED: no target, grants shield to the champion.
- **persistent**: The bank is invisible state accumulating between casts (per slot). BANKED's shield persists (wave-local, half carries per ShieldRules). CARRIED: wave-start shield.
- **movement**: None in Core. On screen the Seeker plays the plain 'trap' clip in place (crouch, hand to the ground, laying a trap).
- **duration**: Instant on the beat; bank builds over the cooldown; CARRIED shield granted at wave ms 0.
- **stacking**: Bank sums bites since last cast (VENGEANCE: only bites within 3s; RAW NERVE 2s). Shield capped by champion shield cap via GrantShield.
- **events**: Bites: EnemyStrike (+ShieldAbsorbed etc.); the bank itself emits NOTHING. Cast: Skill(Slot=i) at beat ms, then Strike(front creature, Amount = payback, Hit=Primary, Crit) (Amount may be 0), EnemyDown on kill. BANKED: Skill then ShieldGained(Amount=added) at the beat, no Strike. CARRIED: ShieldGained at AtMs 0 (screen does not say it, HuntScreen.cs:2436).
- **variations**: VENGEANCE (Shadow): 350%, only last 3s counts -> window/decay of banked damage should be visible; GRUDGE 500%; RAW NERVE 2s window 700%; SCARRED +50% below 40% health (show wounded-hunter boost). BANKED (Machine): becomes SHIELD on the hunter instead of damage -> defensive conversion picture; LINING 300%; STANDING x1.5 shield; CARRIED wave-start shield 5% (card says 10%).
- **current_presentation**: Legacy generic path (no ActionRecipe; ReactionRecipes explicitly excludes it, ReactionRecipe.cs:457). Clip: plain 'trap' clip committed on the beat (NextAnimatedAction -> CommitPlainClip, HuntScreen.cs:6496-6506) -> char_seeker_trap_strip8_512 (hunter crouches and sets something on the ground). Skill event: callout 'SNARE' (Machine orange), PlaySkillVfx -> CastTrap ('cast.trap', EnemyRow, Standing, 0.80 of row width, GroundUnder, 12 fps, VfxProfiles.cs:157-159) with fx_seeker_trap_strip8_512 = a static coiled rope lasso ring under the whole enemy row, tinted by Source; sfx_cast 0.42. Strike: sfx_hit 0.22, skill-graded number, F2 flash, puff. BANKED: same clip/rope/callout, then ShieldGained -> '+N SHIELD' text, sfx_shield_gain 0.40, VfxProfiles.ShieldGain dome on champion (HuntScreen.cs:2431-2444).
- **current_problems**: Reads as laying a rope snare under the whole row, not as paying back damage to one enemy; nothing shows the bank accumulating or its size, so a 0-damage cast and a huge cash-out look the same (a 0 hit still flashes/thuds/puffs). The row-wide ground rope contradicts single-target. Window variants (3s/2s) and SCARRED threshold invisible. BANKED shows a rope under the enemies before a shield on the hunter (wrong subject). CARRIED wave-start shield is silent. Shares 'trap' clip/FxKey with JAWS' legacy route.
- **asset_quality**: char_seeker_trap: 8-frame crouch-and-set clip (decent but semantically a trap-lay). fx_seeker_trap: rope coil, nearly static across 8 frames (little motion), white for tinting. No REPAY sound.
- **proposed_archetype**: composition: hunter-side ACCUMULATION state (bank filling on each bite) + MELEE CONTACT or PROJECTILE cash-out on the front enemy on the beat
- **secondary_archetype**: BANKED: a self-directed defensive conversion (bank -> shield) - new semantic case 'stored-damage-to-shield', reusing the shield VFX; CARRIED is a wave-start shield beat
- **strategy**: SNARE: 'the enemy should regret hitting you' - a delayed retaliation sized by damage taken, or (BANKED) converted to protection.
- **risk**: A visible bank requires reading per-bite health damage from events (EnemyStrike amount minus ShieldAbsorbed) without re-deriving window rules; Core gives no bank/owed event, so a screen-side mirror could drift (VENGEANCE window, SCARRED) - may need an information-only Core report (like ReactionArmed / Marked). Must not collide with JAWS (shares 'trap' Form/FxKey; JAWS is closed). 0-amount casts need a quiet fizzle. CARRIED text/dial mismatch (10% vs 0.05) is a content bug to flag, not a presentation one.
- **showcase_needs**: Seeker fixture, RH_SHOT_SWAP=volley_spray:snare_repay; a wave where the hunter takes several health-damage bites with no shield before the cast (avoid fightshieldbroken/JAWS IRON which stops bites - swap JAWS out, e.g. snare_jaws:field_mire, so bites reach health). Seek Skill on REPAY's slot; pose a small bank vs a large bank; VENGEANCE+RAW NERVE with bites both inside and outside the window; SCARRED with hunter <40% health; BANKED+STANDING+CARRIED to show the shield gain and the wave-start shield.

## sign_call — CALL
- **owner**: shared (any champion; SIGN style)
- **kind**: Active / SkillKind.Active, SkillEffect.Amplify, Style.Sign, Beats 5, Targets 1, AmplifyPercent 0.60, AmplifyMs 6000, AmplifyWholeWave true, ClipKey 'mark', FxKey 'mark' (SkillCatalogue.cs:516-546)
- **gameplay**: On the beat it opens a +60% amplifier on all champion damage to every enemy for 6s. No damage, no target change. SPEND (Mind): +200% (OVERSPEND +300%) on the next 3 (COUNT 5) damaging hits (swing or skill), each hit spends a charge (PERFECT CLAUSE: a crit spends none), window closes when spent (SoloBattle.cs:1419-1431). STEADY (Spirit): no 6s burst; each cast adds +40% for the rest of the wave up to +80% (2584-2595); FOUNDATION primes one stack at wave start with NO event (1166-1175). Offensive support. What travels: nothing physical - an empowerment from the hunter onto his own subsequent hits. Moment to perceive: the call going up (the window opening), then which hits are empowered while it holds, and its end (timeout or last charge spent).
- **trigger**: Takes the champion's action on its beat (5 beats). Sign branch of the cast block (SoloBattle.cs:2562-2615); deals no damage.
- **targets**: The champion's own damage against the WHOLE wave (ampWholeWave true): every hit routed through LandSpread->Amp (skill hits and the basic swing) on any creature is amplified (SoloBattle.cs:1411-1440). Bleed/carry/reflect via LandOn are NOT amplified.
- **persistent**: Yes: a wave-wide amplify window (ampBonus, ampUntil). Base 6s (Linger x1.8, mastery multiplier, per-enemy extension). SPEND: held until 3 (COUNT 5) amplified hits are spent (window 600 000 ms, closes on count). STEADY: lasts the rest of the wave (TickCeilingMs).
- **movement**: None. Seeker plays the plain 'mark' clip in place (extends an arm and points).
- **duration**: 6s base window; SPEND until N hits; STEADY whole wave.
- **stacking**: Base: refreshed/replaced each cast, never stacked. SPEND: hit count refilled per cast. STEADY: +40% per cast (REDOUBLE +70%) up to +80% (PILLAR +160%); FOUNDATION starts the wave with one stack.
- **events**: At the beat: Marked(Slot = amplify percent rounded, Amount = window ms) FIRST, optional Charge, then Skill(Slot=i) (SoloBattle.cs:2611-2615). No event for each spent SPEND charge, none when the count closes the window, none for FOUNDATION's primed stack, none when the window times out. Amplified hits are ordinary Strike events (no 'amped' flag).
- **variations**: SPEND (Mind): +200% on next 3 hits, held until spent -> show discrete charges and each spend; OVERSPEND +300%, COUNT 5 charges, PERFECT CLAUSE crits keep a charge (show a crit not consuming). STEADY (Spirit): +40% per cast for the wave, cap +80% -> a growing standing swell with stack level; REDOUBLE +70%/cast, PILLAR cap +160%, FOUNDATION starts the wave with one stack.
- **current_presentation**: Legacy path, no recipe (MarkRecipes holds only sign_brand; FieldRoles only considers Field skills, MarkRecipe.cs:409-424, so CALL is never a mark-layer skill). Clip: plain 'mark' clip on the beat -> char_seeker_mark_strip8_512 (arm extends and points). Skill event: callout 'SIGN' (Bone), PlaySkillVfx -> CastMark ('cast.mark', Creature, Head, 0.45, Overhead, OffsetY -0.10, VfxProfiles.cs:162-164) with fx_seeker_mark_strip8_512 = a crosshair/reticle ring with a diamond centre over ONE creature's head (castTarget = the next Strike in the batch, usually a coincident swing, else TargetSlot() = front), tinted by Source; sfx_cast 0.42. Marked event only feeds WaveReplay (_markPercent/_markUntilMs, WaveReplay.cs:519-522) -> enemy inspector line 'MARKED   YOUR SKILLS +N% FOR Xs' (HuntScreen.cs:2785). Empowered hits are not distinguished.
- **current_problems**: Draws a single-creature target reticle for a wave-wide buff on the hunter's own damage - wrong subject and wrong reach, and reads like BRAND (which is a curse on the front enemy). Nothing shows the window standing, its expiry, or which hits were amplified. SPEND's inspector reads 'FOR 600s' and keeps saying MARKED after the last charge closes the window (no event); STEADY reads the tick ceiling; FOUNDATION's primed stack is invisible. Inspector text says 'YOUR SKILLS' while the swing is amplified too. Possible confusion with BRAND's per-wave mark builder, which reads a Marked after a BRAND Aura at the same ms (HuntScreen.cs:1603-1606) - a CALL cast on that ms could be misread.
- **asset_quality**: char_seeker_mark: clean 8-frame point/command pose. fx_seeker_mark: crosshair reticle, white for tint; reads as targeting, not empowerment. No CALL sound.
- **proposed_archetype**: new semantic case: SELF-EMPOWERMENT / BUFF STATE on the hunter (a hunter-attached FIELD-AURA-like persistent state that tints/charges his outgoing hits), opened by a short performed call
- **secondary_archetype**: composition: a brief cast gesture (no contact) + persistent hunter state + per-hit empowered-impact accent on amplified Strikes; SPEND = countable charges, STEADY = stacking level
- **strategy**: SIGN: deals nothing itself, makes everything else bigger - a buff on the hunter's output, not a debuff on a creature.
- **risk**: Core does not report charge spends, count closure, FOUNDATION priming or expiry, and Strike events carry no 'amplified' flag; presenting empowered hits honestly likely needs an information-only Core report (as BRAND needed Marked/ReactionArmed) - fingerprint-proof it. Must stay visually distinct from BRAND (MARK/AFFLICTION on enemy) and from PRESS's field. Fast casts (5 beats) refresh the window; avoid strobe.
- **showcase_needs**: Seeker fixture with RH_SHOT_SWAP=volley_spray:sign_call (Game1.cs fightinspect already weaves sign_call, Game1.cs:2888-2892); seek to its Skill event, then a few beats after to show amplified swings/HARD HANDS hits and the expiry at +6s. RH_SHOT_VARIATION=sign_call:SPEND+COUNT+PERFECT CLAUSE (needs crit chance to show a kept charge; enough hits to spend all charges) and sign_call:STEADY+PILLAR+FOUNDATION across 2+ casts in one wave to show stacking; a multi-creature wave to show whole-wave reach.

## volley_weep — WEEP
- **owner**: shared (any champion; VOLLEY style)
- **kind**: Passive / SkillKind.Reaction, On ReactionOn.Kill, SkillEffect.Damage, Style.Volley, Targets WholeWave, BasePower 0, BleedOnKillFraction 0.30, ClipKey 'projectile', FxKey 'weep' (SkillCatalogue.cs:623-650)
- **gameplay**: Delayed, persistent, passive. On a death, invisible pool grows; every half second a fraction bleeds into the front enemy as an armour-ignoring Bleed hit (not amplified by SIGN, not Primary, no crit). Bleed kills feed the pool again (chain). Offensive. What physically travels: the dead enemy's 'blood/essence' onto the survivors (front). TORRENT (Nature): pays out 2x faster (front-loaded); CARRION (Shadow): lingers, sheds ~45% as fast (longer tail); ONSET: the champion's casts add 10% of their raw to the pool (BleedFromHits, gathered from the weave, SoloBattle.cs:1104-1107, applied 2929). Moment to perceive: a death leaving bleed behind, the pool's size, and the periodic ticks on the front enemy.
- **trigger**: Any enemy death from any source (cast, swing, carry, the bleed itself) in LandOn's kill branch (SoloBattle.cs:1810-1823). NOT dispatched by the Kill reaction loop: that gate requires BasePower > 0 (SoloBattle.cs:1897-1900), so WEEP never 'fires' as a skill.
- **targets**: Feeds ONE wave-local poison pool; the pool bleeds into FirstAlive (the current front enemy) every 500 ms, armour-ignoring (SoloBattle.cs:2216-2224) - despite 'on the wave' it is single-target at the front, and retargets as fronts die.
- **persistent**: Yes: the pool persists for the rest of the wave (local `poison`, SoloBattle.cs:916), shared with VENOM, CRIT-to-bleed, overkill-to-bleed and the casts' BleedFromHits. Lost at wave end (CARRION's 'carries' is a slower shed within the wave, not across waves).
- **movement**: None. No champion clip ever plays for it (no Skill event), although ActorClips loads the 'trap' clip for every Reaction (ActorClips.cs:54-58).
- **duration**: Continuous decaying DoT, ticks every 500 ms while pool > 0.5, until wave end.
- **stacking**: Each kill adds 30% of the dead enemy's max health (DRY/DREGS 45%); FLOOD: +50% of that contribution if the pool is already >0. Shed per 500 ms = pool x 0.12 x BleedRate (TORRENT 2, SPILLWAY 3); CARRION sheds 0.054 x rate (LAST DROP 1.5). BleedRate is the max across the weave and also speeds VENOM.
- **events**: No Skill event, no ReactionArmed. The death: EnemyDown(slot) (pool growth is silent). Each 500 ms tick: Strike(Slot=FirstAlive, Amount=bite, Hit=HitSource.Bleed, Crit=false), EnemyDown if it kills. ONSET contributes silently at the cast.
- **variations**: TORRENT (Nature): bleed pays 2x faster -> faster/heavier ticks; DRY 45% per kill; SPILLWAY 3x faster; FLOOD +50% contribution while already bleeding (show a kill adding to a running pool). CARRION (Shadow): slower shed, longer tail -> lingering, thicker state; DREGS 45%; ONSET casts add 10% of raw to the pool (show casts feeding it); LAST DROP 1.5x shed.
- **current_presentation**: Effectively none of its own. Its authored art fx_weep (falling droplets column, CastRain profile 'cast.rain', Creature Head 0.90 Overhead, VfxProfiles.cs:185-187, mapped by FxKeyOverride['weep'] 243) is NEVER played: it only fires from PlaySkillVfx on a Skill event, which WEEP never emits (grep: no consumer besides the table). What the player sees: each Bleed tick goes through the generic Strike case (HuntScreen.cs:2219-2330) as FromSkill: sfx_hit at 0.22 every 500 ms, a plain (non-skill-graded) damage number on the front creature, the usual F2 flash whenever the previous flash has ended, and the ImpactWeak puff every other blow. No callout, no clip, no pool visual, no source link from the dead enemy.
- **current_problems**: Dormant presentation: fx_weep and the WEEP-specific profile never reach the screen; the 'trap' clip is loaded for it and never used. The bleed reads as anonymous periodic thuds/flashes/numbers on the front enemy (a strobe: flash + thud twice a second), indistinguishable from VENOM's bleed. The kill->bleed causality, pool size, TORRENT vs CARRION tempo and FLOOD chain are invisible. Rail: RailCooldownMs gives 1000 ms for it and no ReactionArmed exists - check the rail does not show a fake rearm. Card says 'on the wave' but damage hits only the front.
- **asset_quality**: fx_weep_strip8_512: sparse thin outline droplets, very faint, white; tall narrow column (0.27x0.97). Generic hit sound only. Shared pool means a VENOM build looks identical.
- **proposed_archetype**: TARGET AFFLICTION (persistent bleed state on the front enemy, BRAND-like layer) fed by a death-triggered TARGET REACTION transfer
- **secondary_archetype**: composition: on EnemyDown a short 'blood leaves the corpse and reaches the new front' transfer (death-sourced, not champion-sourced projectile) + a standing affliction with periodic tick accents; ONSET adds a cast->pool feed accent
- **strategy**: VOLLEY target economy: deaths feed damage onto the next enemy - a chain that turns kills into more kills.
- **risk**: Pool is shared with VENOM / CritToBleed / OverkillToBleed and is not reported by Core: Bleed Strikes cannot be attributed to WEEP vs VENOM and pool size is unknown - honest presentation likely needs an information-only Core report (pool after each change, with provenance), fingerprint-proven like BRAND's hook. Ticks every 500 ms must be quiet (no per-tick flash/thud strobe) and yield to SPRAY/HARD HANDS/JAWS/PRESS/BRAND. Must coexist with BRAND's curse on the same front body. Deaths can be deferred by JAWS' snap (_deathDeferred) - the transfer must start from the shown fall.
- **showcase_needs**: Seeker fixture with RH_SHOT_SWAP=snare_jaws:volley_weep (reaction slot); a multi-creature wave (fightmulti) with a kill early so the pool feeds the next front; seek before an EnemyDown (no Skill event exists - DevSeekBefore must aim at EnemyDown or a Bleed Strike), then hold several 500 ms ticks and a bleed-caused kill to show the chain. RH_SHOT_VARIATION=volley_weep:TORRENT+SPILLWAY+FLOOD vs volley_weep:CARRION+DREGS+ONSET (ONSET needs an active damage cast, e.g. SPRAY, in frame). Compare against a VENOM build to show it is distinguishable.

## field_pulse — PULSE
- **owner**: shared (any champion); taught by mastery road_field_2 'prospect' (MasteryCatalog.cs:359)
- **kind**: Active (SkillKind.Active, SkillEffect.Damage, Style.Field), Beats 5, IntervalMs 0, Targets WholeWave, BasePower 140, ClipKey 'aura', FxKey 'aura' (SkillCatalogue.cs:655-683)
- **gameplay**: Instant area damage. The whole hit lands on the cast beat, no delay and no travel. One hit per living creature. The amount is PoweredBase x DamageMultiplier; THRONG multiplies it by (1 + 0.20 x living enemies) (SoloBattle.cs:2774). Nothing persists, nothing stacks, and the target does not change. It is offensive: the wave is hit by a shock from the hunter. The player has chosen a spread burst on a cooldown, so what they must see is that one cast hit every enemy at once, and with SHARE how big each share was.
- **trigger**: Takes a beat: every 5th action (SoloBattle.cs:2489-2514); CROWDED drops 1 beat while >=4 enemies live (SoloBattle.cs:2493)
- **targets**: Every living creature (WholeWave) via LandSpread (SoloBattle.cs:2794, 2829); SHARE splits one pool across at most SplitMaxWays (2795-2815)
- **persistent**: No
- **movement**: None in Core. A field-shock front that crosses from the hunter to the pack is the natural reading
- **duration**: Instant (one beat); the cast clip is about SkillClipShareOfBeat of a beat
- **stacking**: No (THRONG reads the living count at the cast)
- **events**: BattleEvent Skill(slot=i) once per activation at the beat ms (SoloBattle.cs:2641), then one Strike per creature hit at the same ms with Hit=Primary, slot=creature idx (LandOn SoloBattle.cs:1775). Charge if the charge pool is live (2995-2996). Heal if Source=Nature (signature 3% leech, 2989-2990). ECHO/OVERDRAW: one Skill event, the loop runs twice
- **variations**: THRONG (Nature): +20%/living enemy; HORDE 35%; PACKED x1.3; CROWDED one beat sooner at >=4 alive. The picture should get bigger or denser with the crowd. SHARE (Spirit): a 300% pool split evenly over living enemies (99 ways max); POOL 450%; NARROWED at most 2 ways, so only 2 creatures are hit (spreadTargets=ways); BALANCE splits only between creatures holding >= an even share, so it can collapse to 1 target (SoloBattle.cs:2802-2814). The presentation must show WHICH creatures got a share and that the shares are equal, and for NARROWED/BALANCE the hit must not be shown on the whole wave
- **current_presentation**: Generic cast path, HuntScreen.cs:2345-2391. Callout 'FIELD' in Bloom (CalloutFor, HuntScreen.cs:1197-1205). sfx_cast at 0.42. PlaySkillVfx -> VfxProfiles.ForSkill: ClipKey 'aura' -> CastAura (VfxProfiles.cs:141-142, 256): a one-shot fx_aura (shared white flame-ring strip, assets/art/VFX/aura/fx_aura_strip8_512.png, no per-character fx_seeker_aura) centred on the CHAMPION at 1.05x, tinted SourceGlow. Champion clip 'aura' has no seeker strip, so it falls back to char_seeker_cast (Character.cs:186-200). Each Strike (HuntScreen.cs:2219-2331) plays sfx_hit at 0.22, a generic F2 flash on each creature, one skill-graded number per creature (summed per creature), and an ImpactWeak puff on every other blow. Nothing travels from the hunter to the pack, and nothing is drawn on the wave as a whole
- **current_problems**: The picture sits on the hunter, while the effect lands on the whole pack 600+ px away. The same fx_aura flame art is the HELD field art for MIRE, so a PULSE cast and a MIRE tick look alike. The callout says the style ('FIELD'), not the skill. N creatures make N sfx_hit plus N numbers on top of sfx_cast. Nothing shows THRONG's crowd scaling, SHARE's split, or BALANCE's choice of who is worth splitting into. If a Field Aura tick lands on the same ms as the cast, auraAtMs==e.AtMs and PULSE's Strikes are classed as aura blows: silent, unflashed, folded into one aura total (HuntScreen.cs:2216, 2234)
- **asset_quality**: fx_aura: a grey-white outlined flame bubble, 8x512, hand-drawn line art, generic. It reads as a fire shield, not as a radiating shock. char_seeker_cast is the generic cast strip. No skill sound
- **proposed_archetype**: FIELD-AURA (instant cast variant: a field shock emitted from the hunter that arrives on the wave)
- **secondary_archetype**: PROJECTILE-like travel of a front (as in PRESS's crescent) for the reach; per-target contact for SHARE/NARROWED
- **strategy**: Build a skill-keyed recipe (an ActionRecipe for the cast, or FieldRecipe-style front parts) on the PRESS field vocabulary: a compression at the hunter on release, then one wide pressure wave that travels the row and lands on the beat over every creature the batch's Strikes name (ActionTargets). Keep the targets per event so NARROWED/BALANCE light only the struck creatures. One contact cue and one combined number per creature (no 0.22 sfx_hit per creature). Scale wave amplitude by the struck count for THRONG. Give it its own effect key rather than sharing fx_aura with MIRE
- **risk**: Medium: the same-ms classification bug with Field ticks; NARROWED/BALANCE must not show a whole-wave hit; numbers spam on large waves
- **showcase_needs**: Seeker with PULSE in a slot (RH_SHOT_SWAP=<fixture skill>:field_pulse), against a Swarm wave of >=4 creatures to pose CROWDED/THRONG. Variants via RH_SHOT_VARIATION=field_pulse:THRONG(+HORDE/PACKED/CROWDED) and field_pulse:SHARE(+NARROWED/BALANCE) on a wave where some creatures are low, so that BALANCE collapses. Seek to the 5th beat (the first cast). Also pose one wave beside MIRE so that a tick and a cast fall on the same ms

## field_mire — MIRE
- **owner**: shared (any champion); taught by mastery road_field 'weigh' (MasteryCatalog.cs:354); also the migration target for legacy Aura saves (LegacySkillForm.cs:34)
- **kind**: Field (SkillKind.Field, SkillEffect.Damage, Style.Field), IntervalMs 1000, Beats 0, Targets WholeWave, BasePower 12/s, SlowFraction 0.25, ClipKey 'aura', FxKey 'aura' (SkillCatalogue.cs:685-717)
- **gameplay**: A persistent, always-on field. Each tick does two things. (1) It sets the wave-wide slow: slowFactor = max(held, want), where want = 0.25 + NUMB deepen x ticks + TEEMING per living enemy, capped at the ceiling. The slow never relaxes during the wave (SoloBattle.cs:2367-2392). It stretches the interval between ALL bites: nextBite += interval x (1+slow) (3062-3072). (2) It deals BasePower x interval/1000 x DamageMultiplier to every creature, no beat taken (2459-2478). With Source=Nature it also heals 3% of what it dealt (2475-2476). It is defensive (the wave bites less often) with a small damage drip. Nothing travels: the effect is a mud/drag on the enemies, and the player must see that the wave is BOGGED, that their attacks come more slowly, and that the slow deepens
- **trigger**: Its own clock: every IntervalMs (x3/5 with RADIANCE), in arrears, so the first tick comes one interval into the wave (SoloBattle.cs:2253-2263)
- **targets**: Whole wave: a slow on the wave's single bite clock, plus damage to every living creature (SoloBattle.cs:2471-2473)
- **persistent**: Yes (the slow holds and only rises within the wave; resets each wave)
- **movement**: None. A ground mire under the pack and a visible drag on their bite wind-up
- **duration**: The whole wave; ticks every 1000 ms (600 with RADIANCE)
- **stacking**: The slow is max(held, want); NUMB deepens per tick to its ceiling; TEEMING rises with the living count, but the grip holds after deaths
- **events**: Per tick: Aura(slot=i) (SoloBattle.cs:2267), then Slowed(slot=-1, amount=standing slow %) (2390), then Strike per creature (Primary), then possibly Heal (Nature). REMNANT emits nothing of its own; it lowers incoming damage in the bite calculation (3110-3111)
- **variations**: NUMB (Nature): +5%/tick to a 40% cap; DEEPEN +10%/tick to 50%; SEDIMENT ceiling 55%; SILT damage x1.5. The presentation must show the depth growing over ticks. TEEMING (Spirit): +6% per living enemy to 60%; CLOG 10%/enemy to 78%; BRIM base 40% with an 85% cap; REMNANT: the wave bites 5% softer per dead enemy, to -40% (SoloBattle.cs:1063-1079, 3110). The presentation must show the dead weighing on the living (corpses staying in the mire)
- **current_presentation**: Generic HELD aura (FieldRoles.Choose -> Held, MarkRecipe.cs:409-425; HuntScreen.cs:1509-1516). HoldAura (HuntScreen.cs:7376-7389) holds VfxProfiles.FieldAura (VfxProfiles.cs:216): fx_aura, BEHIND the CHAMPION, Standing anchor 1.10x, tinted with the Source colour. Brightness is rest 0.38 with a cubed spike to 1.0 over 0.42 s. The spike comes from PulseAuraOnClock on a WALL-CLOCK 1 s metronome (HuntScreen.cs:7344-7348), NOT from the Aura events. The tick's Strikes are auraTick: no sound, no flash, no puff, and ONE summed number over TargetSlot flushed later (HuntScreen.cs:2234-2258, 7486-7491). The slow appears only in the hover inspector as 'SLOWED +N% BETWEEN BITES' and in 'BITES EVERY' (HuntScreen.cs:2763-2782; WaveReplay.cs:512). No sound at all
- **current_problems**: The flame halo wraps the HUNTER, but the mire acts on the ENEMIES, so nothing on the wave shows they are slowed. The flare is on a wall clock and drifts out of step with the real 1 s / 600 ms (RADIANCE) ticks. The slow's depth (NUMB deepen, TEEMING crowd, ceiling) is invisible. The bite lunge does not visibly drag. Nature's heal makes a HealColumn and '+N' every tick (HuntScreen.cs:2393-2401). HoldAura returns early when any performed field (_field, i.e. PRESS) exists (HuntScreen.cs:7381), so MIRE beside PRESS shows NOTHING at all. FieldRoles keeps only the FIRST plain field as Held, so MIRE plus WILT shows only one of them. The same art as PULSE's cast
- **asset_quality**: fx_aura shared white outlined flame bubble (generic, reads as fire, not as mud or drag); no sound
- **proposed_archetype**: FIELD-AURA (persistent ground field over the ENEMY row)
- **secondary_archetype**: TARGET AFFLICTION (wave-wide slow state on each creature; a drag on the bite wind-up)
- **strategy**: A FieldRecipe keyed (champion,'field_mire'). It needs a FieldRoles change so that a second recipe field is not lost: today only one Performed slot exists, and Held is suppressed by _field. Build it from the Aura events with each tick's Slowed amount: a mire/sediment band on the ground under the pack (GroundUnder layer), and a level that rises with Slowed% (NUMB, TEEMING). Each tick is a soft pulse through the band, timed to the event, not the wall clock. Slow the creatures' wind-up visually (BitePresentation lead stretch already follows the replay's bite times; add a sink/drag pose or mud tendrils on the legs while slowed). Keep the tick's damage as one quiet number. Give REMNANT corpse residue in the mire. Needs a quiet tick cue under PRESS levels, and must not take a beat
- **risk**: High: the field-role arbitration (one Performed slot; the held aura is hidden by PRESS); legacy saves land on MIRE, so it is the most-played field; the Nature heal column every tick
- **showcase_needs**: Seeker with field_mire (fixtures fightaura/fightinspect already weave it, Game1.cs:2816-2820, 2888-2892) on a 4-5 creature wave, filmed for >=5 s so NUMB reaches its ceiling. RH_SHOT_VARIATION=field_mire:NUMB+DEEPEN, field_mire:TEEMING+CLOG, and field_mire:TEEMING+REMNANT with kills mid-wave. Pose beside PRESS (to prove both draw) and beside WILT; pose with RADIANCE for the 600 ms clock; Nature source to check heal spam

## drain_drink — DRINK
- **owner**: shared (any champion); taught by mastery road_drain 'bulk' (MasteryCatalog.cs:303)
- **kind**: Active (SkillKind.Active, SkillEffect.Heal, Style.Drain), Beats 6, Targets 1, BasePower 260, Lifesteal 0.12 (HealTuning.TransformationLeech), ClipKey 'transformation', FxKey 'transformation' (SkillCatalogue.cs:720-749)
- **gameplay**: An instant, heavy single-target blow on the beat. Then, in the same ms, it heals the champion by dealt x Lifesteal through Heal(), which obeys the per-wave ceiling, starting at 40% of max health (SoloBattle.cs:2937-2943, 2119-2187). With SIPHON (an enchantment trigger) the leech is x2 and the ceiling x1.5 (1140). It is offensive and sustaining. Life flows from the enemy BACK to the hunter, so what physically travels is the health returning. The player must see the bite on one enemy, and the life drawn back into the hunter in proportion to the hit
- **trigger**: Takes a beat: every 6th action (SoloBattle.cs:2489-2514)
- **targets**: The single front living creature (LandSpread, targets 1, SoloBattle.cs:2829)
- **persistent**: No
- **movement**: Core has none. The natural reading is a contact on the target, then a stream or orbs travelling target -> hunter (reverse projectile)
- **duration**: Instant (one beat); heal same ms
- **stacking**: No (the per-wave heal ceiling bounds it)
- **events**: Skill(slot=i) at the beat (SoloBattle.cs:2641); Strike on the front creature (Primary; 1775); Heal(slot 0, amount landed) after it (2187), only if the ceiling has room. Nature source adds a second signature heal (2989-2990). Charge point
- **variations**: SIPHON (Machine): lifesteal x2 (0.24); PUMP x1.5 leech; PARCH dmg x1.3; TRICKLE basic swings also lifesteal 3% (SoloBattle.cs:1115-1121, 3024-3025). TRICKLE adds Heal events on ordinary swings, so a tiny return on each swing is needed. GLUT (Nature): Lifesteal 0, damage x(1 + 1.5 x health share) (2760-2771); SURFEIT 3.0; STOUT x1.3; RIPE x1.5 scaling above 90% health. There is no heal at all, so the picture must be a fed, swollen blow rather than a drink, scaled by the hunter's health
- **current_presentation**: Generic cast path (HuntScreen.cs:2345-2391). Callout 'DRAIN' in Verdant. sfx_cast at 0.42. CastTransformation (VfxProfiles.cs:171-172): a one-shot FxFor -> fx_seeker_transformation_strip8_512 (a per-character grey twisted-spiral column) centred ON THE CHAMPION at 1.05x, Source-tinted. Champion clip char_seeker_transformation (a cape-swirl, fist-forward pose). Strike: sfx_hit at 0.22, F2 flash, skill-graded number, puff. Heal: Say('+N', Verdant) and a HealColumn one-shot on the champion (HuntScreen.cs:2393-2401). Nothing links the enemy to the hunter
- **current_problems**: The transformation column reads as the hunter powering up, not as drinking from the target. There is no contact on the enemy beyond the generic flash, and no return flow, so the heal looks unrelated to the hit. Two heal events (lifesteal plus Nature) make two columns and two '+N'. Once the per-wave ceiling is spent, the Heal event vanishes and the cast silently stops healing, with no feedback. GLUT (no lifesteal) still plays the same 'transformation' picture. SIPHON variation text promises the per-wave limit doubles, but the variation only doubles Lifesteal; the ceiling is raised only by BuildTrigger.Siphon (the enchantment/mastery trigger, SoloBattle.cs:1140; HealTuning.cs:129-134), so that half of the card is apparently not turned by the variation
- **asset_quality**: fx_seeker_transformation: a dense grey spiral-lines column (busy, abstract). char_seeker_transformation: a usable 8-frame clip but a flourish, not a drink. fx_heal column is generic. No skill sound
- **proposed_archetype**: composition: MELEE CONTACT or PROJECTILE (the bite on one target) + a reverse travel (life returning to the hunter)
- **secondary_archetype**: TARGET REACTION on the drained enemy (a pallor or a pulled-out wisp); GLUT is a pure contact blow
- **strategy**: Add a skill-keyed ActionRecipe for (champion,'drain_drink') on the HARD HANDS / SPRAY action model: a release, a contact on the front creature on the beat, then a return stream whose arrival on the hunter is timed to the batch's Heal event. Scale the stream by the healed amount and drop it when no Heal follows (ceiling spent or GLUT). Replace the generic HealColumn and '+N' with the stream's arrival when it is this cast's heal (same-ms Heal after this Skill). The per-variation look: GLUT has no stream and a health-scaled contact; TRICKLE gets a small sip on swing Heals. One release cue and one contact/drink cue, ducking others
- **risk**: Medium: the heal may be clamped to 0 (no event) and must not be faked; the double heal from the Nature signature; possible SIPHON card/implementation mismatch (the ceiling half)
- **showcase_needs**: Seeker with drain_drink (RH_SHOT_SWAP) against a single tanky creature (Bruiser) so several casts land. The champion is pre-damaged so heals are visible, and one wave is long enough to exhaust the 40% ceiling. Seek to the 6th beat. Variants: drain_drink:SIPHON+TRICKLE (swing heals), drain_drink:GLUT+RIPE at full health; Nature source to show the double heal

## drain_wilt — WILT
- **owner**: shared (any champion); taught by mastery road_drain_2 'stand' (MasteryCatalog.cs:317)
- **kind**: Field (SkillKind.Field, SkillEffect.Heal, Style.Drain), IntervalMs 1000, Beats 0, Targets WholeWave, BasePower 0, AttackBreakPerTick 0.10, AttackBreakFloor -0.50, ClipKey 'transformation', FxKey 'wilt' (SkillCatalogue.cs:751-790)
- **gameplay**: A persistent field. It deals NO damage: it continues before the damage path (SoloBattle.cs:2394-2424). Each tick deepens an attack break by 10%, floored at -50%. At the bite, the wave's summed incoming damage is multiplied by (1+attackBreak) (3097). SHRIVEL instead breaks only the creature in front (by reference at bite time, so a new front inherits it) by 25%/tick to -90%, and HOLLOW also breaks the second at half rate (3087-3090). SUP heals 1% of max health per tick through the ceiling (2421-2422). It is defensive: the enemies wither and bite softer, and sometimes life seeps to the hunter. Nothing hits. The player must see the enemies' strength draining and how deep it is
- **trigger**: Its own clock every 1000 ms (RADIANCE x3/5), in arrears (SoloBattle.cs:2253-2263)
- **targets**: The whole wave (base and SUP); SHRIVEL: the front creature only (+ the second at half depth with HOLLOW)
- **persistent**: Yes (the break deepens to its floor and holds for the wave; resets per wave; SHRIVEL's break follows the front by position)
- **movement**: None. A draining wisp from enemy to hunter is only justified on SUP
- **duration**: The whole wave; ticks every 1000 ms
- **stacking**: Deepens per tick to the floor (AttackBreakPerTick, AttackBreakFloor); not stacked across waves
- **events**: Per tick: Aura(slot=i) (2267), then AttackBreak(slot=-1 whole wave | 0 front | 1 second, amount=standing % negative) (2403, 2410, 2416), then Heal(slot 0) when SUP has room. No Strike
- **variations**: SUP (Body): heal 1% max health per tick; BROOK floor -70%; BALM 1.5%/tick and ceiling +40% (SoloBattle.cs:1145-1147); RESERVE 15%/tick. The presentation must show the heal seeping back to the hunter. SHRIVEL (Mind): front only, 25%/tick to -90%; HOLLOW second enemy at half; SEIZED 35%/tick; GAUNT floor -97%. It must be a deep wither concentrated on ONE body (two with HOLLOW), migrating to the new front after a death
- **current_presentation**: Generic HELD aura via FieldRoles Held (HuntScreen.cs:1509-1516): VfxProfiles.FieldAura with FxFor('wilt') -> shared fx_wilt (a white ring with three drops; no per-character strip), held BEHIND THE CHAMPION, Standing 1.10x, Source colour. The wall-clock 1 s flare, not tied to the ticks (HuntScreen.cs:7344-7389). No sound, no number (no Strike). The break is visible only in the hover inspector as 'ATTACK BREAK N%' and in the ATTACK base -> now row (HuntScreen.cs:2763-2780; WaveReplay CreatureAttackBreakPercent). SUP: HealColumn and '+N' every tick
- **current_problems**: The withering is drawn on the HUNTER, not on the enemies it weakens, and the depth (-10..-50/-97%) is invisible in the arena. SHRIVEL (front only) looks identical to the whole-wave base. The flare is out of sync with the real ticks. With PRESS present, HoldAura is suppressed (_field != null, HuntScreen.cs:7381), and with MIRE woven first it is not chosen as Held, so the build shows nothing. SUP's per-second heal column is noisy and stops silently when the ceiling is spent. The bite numbers do drop, but nothing ties that to WILT
- **asset_quality**: fx_wilt: an outlined ring with three hanging droplets, generic hand-drawn line art. A symbol, not a phenomenon; it reads as a UI glyph
- **proposed_archetype**: TARGET AFFLICTION (a withering state on the enemy bodies, depth-staged like BRAND)
- **secondary_archetype**: FIELD-AURA (the tick cadence); a reverse-travel heal wisp for SUP
- **strategy**: A skill-keyed recipe (MarkRecipe/curse-style affliction, or a FieldRecipe with per-creature state) built per wave from the Aura and AttackBreak events: a withering/desaturation that grows on the affected bodies as the % deepens (whole row for base/SUP; front, plus second at half, for SHRIVEL/HOLLOW, migrating with the front via the replay's falls). Each tick is a small contraction or droop timed to the event. The enemy's bite contact is visibly weaker (a smaller lunge or flash on wilted biters). SUP gets a thin life wisp arriving at the hunter on the Heal event, replacing the generic HealColumn. Needs FieldRoles to support several presented fields (PRESS + WILT + MIRE) instead of one Performed plus one Held. A quiet tick cue, ducked under actions
- **risk**: High: field-role arbitration; the visual must track the per-creature depth exactly from AttackBreak events (SHRIVEL by front position); the SUP heal can drop to no event when the ceiling is full
- **showcase_needs**: Seeker with drain_wilt (fixture fightinspect already weaves it, Game1.cs:2888-2892) on a multi-creature wave lasting >=5 s, so the break reaches its floor. RH_SHOT_VARIATION=drain_wilt:SUP+BALM with a damaged champion; drain_wilt:SHRIVEL+HOLLOW with a kill of the front creature mid-wave (migration); drain_wilt:SHRIVEL+GAUNT on a single boss. Pose beside PRESS and beside MIRE to prove arbitration

## sig_anvil_hardface — HARDFACE
- **owner**: anvil (THE ANVIL, CharacterRoster.cs:68-69; signature-only: BuildComposer.cs:194, LoadoutRepair.cs:40 refuse it on any other champion)
- **kind**: Active, Style.Hammer, SkillEffect.Damage, Beats 6, IntervalMs 0, Targets 1, BasePower 470, ClipKey/FxKey 'strike', DefenceBreakFloor -48, Rules.DefenceBreakOnKill 12 (SkillCatalogue.cs:841-847)
- **gameplay**: Two parts. (1) The cast: on its beat, one heavy instant blow on the front enemy through LandSpread (SoloBattle.cs:2849-2851). All hits land on the same millisecond; nothing travels. (2) A passive rider that stays on while the skill is woven: ANY kill (cast, swing, carry, bleed) strips 12 defence from every surviving enemy for the rest of the wave, floored at -48 (SoloBattle.cs:1825-1840). It stacks per kill and the target changes: the survivors, not the killed enemy. Offensive, and it softens the enemy for the whole build. PEENING also strips the front enemy on every cast hit, not only on a kill (SoloBattle.cs:2838-2848). UPSET drops the strip and turns the cast into a multi-hit across the whole wave. Player decision: commit a heavy hitter whose kills chain softness. What the player must see: the heavy blow on the beat, and then each survivor visibly 'softening' when something dies.
- **trigger**: Every 6th champion action (beat-counted cast, SoloBattle.cs:2493-2513; opening on beat N-1)
- **targets**: Base: the first living enemy. UPSET: the whole wave (Targets = WholeWave), 2 or 3 hits each
- **persistent**: Yes: the defence strip persists on each survivor for the wave (WaveCreature.Defense), shown as a badge.
- **movement**: None in Core (the blow lands where the creature stands). Presentation: a candidate leap-in melee like HARD HANDS.
- **duration**: Cast resolves instantly at the beat; the clip is about 0.9 of a beat. The strip lasts for the rest of the wave (state).
- **stacking**: -12 per kill (-24 PLANISH, -40 COLD SET), to a floor of -48 / -90 / -150 (BEDPLATE). The Break stack count rises per strip and stops emitting at the floor.
- **events**: Skill(slot) at the cast ms (SoloBattle.cs:2641), then Strike(creature, dmg, Hit=Primary, crit) per hit at the same ms (1775); EnemyDown on a kill (1784). On every kill, for each survivor whose defence moved: Break(creature, stacks) + DefenceNow(creature, defence) at the kill's ms (1838-1840). PEENING: Break + DefenceNow on the front enemy at the cast ms (2845-2846). The champion's DEADWEIGHT passive also emits DeadweightStored/Released.
- **variations**: PLANISH (Machine): strip 24, floor -90; +BEDPLATE floor -150, +COLD SET strip 40, +PEENING strips the front enemy on every cast hit (a strip without a kill must be shown on the target). UPSET (Body): no strip; WholeWave x2 hits at 45% (cast must read as a weight falling on every enemy); +THIRD DROP 3 hits, +RUNOUT leftover falls retarget on death, +UNDERFOOT +75% under half health.
- **current_presentation**: No recipe (ActionRecipe.cs:505-508 only has seeker). A plain clip, 'strike' -> char_anvil_strike_strip8_512, committed on the beat by UpdateChampionClip (HuntScreen.cs:6496-6512). On the Skill event (HuntScreen.cs:2345-2400): a callout (CalloutFor Hammer), PlaySkillVfx -> VfxProfiles.ForSkill -> CastStrike (fx at the creature's centre, scale 0.55, 12 fps, VfxProfiles.cs:174) with FxFor('strike') = fx_anvil_strike_strip8_512 tinted SourceGlow, on the first Strike's creature only, and sfx_cast 0.42. Each Strike: sfx_hit 0.22, the usual F2 flash, a summed number '-N xk' graded as a skill, ImpactWeak on every other blow. The defence strip shows only as a standing badge, icon_effect_break 'xN' over each creature (DrawBreakBadge, HuntScreen.cs:4157-4163), plus the enemy inspector. No case for Break in the event switch, so no moment, sound or VFX when the strip lands.
- **current_problems**: The strip, the whole identity of the skill, has no moment: survivors silently get a badge count when an enemy dies. fx_anvil_strike is a crescent-moon arc (a slash), not a hammer weight. UPSET hits every enemy 2-3x but the VFX plays once on the first target. A generic 8-frame plain clip with no authored contact. No skill-specific sound.
- **asset_quality**: char_anvil_strike / cast / attack / idle strips exist (assets/art/Animations/Roster/anvil_*). fx_anvil_strike_strip8_512 is a static-looking white crescent, a moon/slash shape that misreads for a hammer. No sounds of its own (sfx_cast/sfx_hit generic). icon_skill_sig_anvil_hardface.png exists.
- **proposed_archetype**: MELEE CONTACT (the cast) composed with TARGET AFFLICTION (the kill-strip on the survivors)
- **secondary_archetype**: FIELD-like propagation from the corpse to the survivors on EnemyDown (a death-triggered spread); UPSET is a multi-target melee/slam
- **strategy**: Two layers. (a) A MeleeActionRecipe keyed (anvil, sig_anvil_hardface) on its own strip (a hammer blow, per HARD HANDS: BySkill, own strip, contact on the beat, its own commit and hit cues). UPSET needs a variant: a slam or shockwave reaching every creature with per-target contact, 2-3 pulses. (b) A 'soften' presentation driven by the existing Break/DefenceNow events at the kill ms: a brief crack/dent pulse from the dying creature to each survivor, and a persistent lightweight state (a cracked plating overlay or dimmed material) that deepens with the stack and stops at the floor. Reuse the badge only as the inspector-level readout. A small 'crack' cue once per kill, never per survivor. PEENING draws the same crack on the front target at the cast contact. No Core change is needed: Break/DefenceNow already carry slot, stacks and value.
- **risk**: The kill-strip fires on kills from ANY source (the swing, bleed, carries), so its presentation must not depend on the cast being the killer, and may coincide with other death VFX. At the floor no event fires, so the visual state must hold, not pulse. Many kills in a swarm means many strip moments: needs throttling or merging per ms.
- **showcase_needs**: RH_SHOT_HUNTER=anvil (the signature is ensured by Game1.cs:2451-2467). A wave of 3+ creatures so a kill leaves survivors. Posed seeks: (1) the cast beat (contact), (2) the first EnemyDown with its Break events (strip propagation), (3) a later frame showing 2+ stacks, (4) a frame at the floor. RH_SHOT_VARIATION=sig_anvil_hardface:PLANISH+PEENING for the strip on a non-kill hit; sig_anvil_hardface:UPSET+THIRD DROP for the whole-wave multi-hit.

## sig_chorus_grave_song — GRAVE SONG
- **owner**: chorus (THE CHORUS, CharacterRoster.cs:86-87)
- **kind**: Field (passive slot), Style.Field, SkillEffect.Damage, IntervalMs 2000, Targets WholeWave, BasePower 16 (per second), ClipKey/FxKey 'aura', DamagePerDeadEnemy 0.35 (SkillCatalogue.cs:874-879)
- **gameplay**: A damage field: every 2s it hits the whole wave at once, instantly. Damage per tick = BasePower x interval s x DamageMultiplier, multiplied by (1 + 0.35 x enemies already dead this wave) at the Amp read (SoloBattle.cs:1512), so it grows louder as the wave empties: the mirror of the champion's MANY MOUTHS passive (+5% per LIVING enemy). Nothing travels, nothing persists on the enemies. CHANTRY instead keeps the plain weight (x1.6) and makes the WAVE BITE SOFTER per dead enemy (14%/death, cap 25%), read at the enemy bite (SoloBattle.cs:1063-1070, 3110-3111) with NO event. SHROUD ignores defence. Player decision: kill the front fast to make the song loud vs keep the crowd alive for MANY MOUTHS. What the player must see: each tick hitting all enemies, and how loud the song is (the dead count).
- **trigger**: Its own clock: every IntervalMs, in arrears (the first tick lands one interval into the wave), takes no beat (SoloBattle.cs:2253-2263)
- **targets**: Every living enemy each tick (LandSpread with TargetsFor(WholeWave), SoloBattle.cs:2468-2470)
- **persistent**: The field is always on; no persistent per-enemy state. The intensity state (the dead count) persists within the wave.
- **movement**: None (a field that reaches every enemy instantly).
- **duration**: Instant tick every 2 s (1.5-4 s by variation); presence held for the whole wave.
- **stacking**: +35% per dead enemy (70% REQUIEM, 110% DIRGE), linear and uncapped within a wave. CHANTRY: -14%/-22% per dead on the enemy bite, capped at 25%/45%.
- **events**: Aura(slot, 0, ms) before the tick's blows (SoloBattle.cs:2264), then Strike(creature, dmg, Hit=Primary) for every living creature at the same ms; EnemyDown on kills. No Skill event (fields never cast). No event carries the dead-count multiplier; CHANTRY's softening emits nothing (it shows only as smaller EnemyStrike amounts).
- **variations**: REQUIEM (Shadow): every 4 s, x2.2, 70%/dead (a slower, heavier toll); +DIRGE 110%/dead, +TOLLING every 3 s, +OPEN GRAVE +90% under 45% health. CHANTRY (Spirit): x1.6 plain, no per-dead damage; the wave bites softer per dead (needs a visible 'subdued enemy' cue); +HUSH cap 45%, +BLACK VEIL 22%/dead, +SHROUD ignores defence.
- **current_presentation**: FieldRoles.Choose (MarkRecipe.cs:412-424) gives it no performed or mark recipe, so it is the HELD field: _auraFxKey 'aura', colour SourceColor[source] (HuntScreen.cs:1515-1516). HoldAura (HuntScreen.cs:7376-7390) holds VfxProfiles.FieldAura (behind the champion, standing, 1.10x height, 10 fps, held) with FxFor('aura') = shared fx_aura (no fx_chorus_aura exists), at a rest brightness with a spike on a FIXED 1 s clock (PulseAuraOnClock, AuraPulseSeconds = 1, HuntScreen.cs:7316, 7344-7349), not on the 2 s/3 s/4 s tick. On the tick's Strikes (auraTick, HuntScreen.cs:2229-2260): no sound, no flash, no puff, no lunge; the tick's damage is summed into ONE number printed over TargetSlot() (the front creature) by FlushAuraTotal. No champion clip.
- **current_problems**: Nothing reaches the enemies: a whole-wave hit is drawn as a glow behind the champion plus one number over the front creature. The glow's brightness peak is on a 1 s clock unrelated to the real interval (2/3/4 s), so the peak usually does not coincide with the hit. The skill's identity (louder per dead) is invisible. CHANTRY's softening is invisible. No sound at all. Shared art with every other plain field.
- **asset_quality**: Shared fx_aura_strip8_512 (line-art sigil loop, FIELD/AURA contract, spec.json archetypes); no chorus-specific aura strip. fx_chorus_strike/projectile/trap/mark/transformation exist but are not used by this skill. Chorus char strips exist. No sound.
- **proposed_archetype**: FIELD-AURA (whole-wave instant tick) with an escalating intensity state
- **secondary_archetype**: For CHANTRY, a wave-wide TARGET AFFLICTION (a weakened bite) read off the dead
- **strategy**: A FieldRecipe keyed (chorus, sig_chorus_grave_song) built per wave from its Aura events (as PRESS does in HuntScreen.cs:1520-1560), but whole-wave: a sung wave/ring released from the champion that reaches every creature on the tick ms, with per-creature contact (a light flash/buckle on each), no travel delay past the tick. Intensity from the dead count: the presentation can count EnemyDown events this wave (already in LastWaveEvents) to scale the voice and the visual (more voices, a brighter or wider front) without a Core change. A tick cue modelled on the PRESS tick (once per tick, quiet under actions), pitched/layered by intensity. The number: one summed number is acceptable but should sit at the wave, not the front creature. CHANTRY: a persistent dimming or hush on the creatures plus a visible softening on their bite, keyed to deaths; Core emits nothing, so either derive it from EnemyDown + the def (the rule is presentation-readable) or add an information-only event (ADR-011 has precedent: Marked, ReactionArmed).
- **risk**: The tick's blows coincide with casts and swings (the 'auraTick' classification exists for that). Whole-wave contact plus kills on the tick produce many simultaneous death VFX. Intensity scaling must not grow louder than the gold-standard actions. The chorus could also weave a shared field: only one held field is drawn (FieldRoles takes the first), and a performed GRAVE SONG must coexist with a held one.
- **showcase_needs**: RH_SHOT_HUNTER=chorus. A wave of 4-5 creatures, posed at: the first tick (0 dead, quiet), a tick after 2 deaths (louder), a late tick (loud). RH_SHOT_VARIATION=sig_chorus_grave_song:REQUIEM+DIRGE (heavy slow toll) and :CHANTRY+HUSH (the softened bite shown on an EnemyStrike after deaths). A shot where a tick shares a ms with a cast (quiet-mode rule).

## sig_metronome_clockwork — CLOCKWORK
- **owner**: metronome (THE METRONOME, CharacterRoster.cs:100-101)
- **kind**: Active, Style.Volley, SkillEffect.Damage, Beats 0, IntervalMs 7000, Targets 3, BasePower 330, ClipKey/FxKey 'projectile' (SkillCatalogue.cs:907-911)
- **gameplay**: An instant multi-target ranged cast: on the beat after its clock, every target is hit at the same ms (LandSpread, SoloBattle.cs:2849). Several 'shots' in Core are just simultaneous hits with no flight time. Offensive. Its identity is TIME: a haste build cannot hurry it, so the player must perceive that it arrives on its own clock (a ticking clock reaching zero) and then strikes across the wave. OFF BEAT retargets leftover shots on death; STEADY HAND ignores defence; LATE BEAT +110% under 40%.
- **trigger**: A wall clock, not a beat count: eligible when abs >= ReadyAt, then it LANDS ON THE NEXT BEAT and spends that action (SoloBattle.cs:2515-2531). The clock is deliberately not divided by the action rate; seeded per wave, never earlier than the clock already standing, so it can carry over a wave boundary (1208-1226). The FIRST BEAT passive waives the opening wait.
- **targets**: Base: the first 3 living enemies, 1 hit each. ROLL: 2 enemies x 2 hits (3 with RIM SHOT). HELD NOTE: the whole wave, with MinimumHits 3 doubling up on a thin wave (SPARE SHOT, SoloBattle.cs:2789-2790)
- **persistent**: No.
- **movement**: Core: none (instant). Presentation: projectiles from the champion to each target (as SPRAY's fan, one per target hit, landing on the beat).
- **duration**: Instant resolution at the beat; a 7 s clock (6.5 ROLL, 9/7 HELD NOTE).
- **stacking**: No stacking. Multi-hit per target (ROLL 2/3).
- **events**: Skill(slot, 0, ms) once per activation (SoloBattle.cs:2641, before the ECHO/OVERDRAW loop; OVERDRAW adds a pass because Style is Volley), then Strike(creature, dmg, Primary) per hit per target at the same ms; EnemyDown on kills. Nothing marks the moment the clock comes round (only the beat it lands on).
- **variations**: ROLL (Machine): 2 targets x 2 hits, x1.15, every 6.5 s (shows as a double strike per target); +RIM SHOT 3 hits, +OFF BEAT leftover shots retarget on death (a visible redirect), +STEADY HAND ignores defence. HELD NOTE (Mind): every 9 s, the whole wave, one 'arrival' (should read as one big chime falling on all, not shots); +WHOLE BAR every 7 s, +LATE BEAT +110% under 40%, +SPARE SHOT minimum 3 hits on a thin wave (extra hits doubling up on 1-2 enemies).
- **current_presentation**: ActionRecipes.For: no BySkill entry, and ByForm only has ('seeker','projectile'), so no recipe for metronome (ActionRecipe.cs:505-528). A plain 'projectile' clip -> char_metronome_projectile_strip8_512 on the beat. Skill event: a Volley callout, PlaySkillVfx -> CastProjectile (VfxProfiles.cs:137-139: from the champion, scale 0.28, Travel ToTarget) with fx_metronome_projectile_strip8_512, flown ONCE to castTarget = the creature of the first Strike in the batch (HuntScreen.cs:2380-2384), and sfx_cast. Each Strike: sfx_hit 0.22, the F2 flash per creature, a summed number per creature ('-N x2' for ROLL). The rail shows its refill over IntervalMs (RailCooldownMs, HuntScreen.cs:7202-7205).
- **current_problems**: Three (or wave-wide) simultaneous hits are drawn as one projectile to one creature; the other targets just flash and print numbers with no shot reaching them, and the projectile arrives after the damage (the hits are on the beat; the strip flies for ~1 s). The 'on a clock' identity is not shown in the arena (only the rail). HELD NOTE ('one arrival on the whole wave') looks the same as the base. A generic cast sound.
- **asset_quality**: fx_metronome_projectile_strip8_512: a grey dart/fin projectile, nearly static between frames (internal motion barely present; it would fail the PROJECTILE 'internal motion' contract in spec.json). Metronome char strips exist (projectile, cast, attack, idle...). No skill sounds of its own.
- **proposed_archetype**: PROJECTILE (a SPRAY-like fan, one projectile per target, contact on the beat)
- **secondary_archetype**: A clock telegraph (an anticipation state on the champion as the clock nears zero); HELD NOTE is closer to a whole-wave FIELD-style arrival
- **strategy**: A ProjectileActionRecipe keyed BySkill (metronome, sig_metronome_clockwork), following the SPRAY contract (one release, one projectile per creature actually hit, landing on the beat, contact ticks for multi-hit) with a composed projectile per ADR-010 (a clockwork bolt/gear head, the runtime trail), not the static strip. Variation-aware: ROLL = 2 projectiles each landing twice (or two volleys staggered inside the contact window); HELD NOTE = one arrival over the whole wave (a bell/pendulum strike descending on every creature) rather than shots. Add a presentation-only clock telegraph: the time to ReadyAt can be derived from the previous Skill event + IntervalMs (the rail already does this), e.g. a ticking cue or a hand sweeping on the champion in the last ~600 ms. Its own release/contact cues.
- **risk**: SPRAY's ByForm recipe for 'projectile' is Seeker-only, so no leak, but the metronome's other Volley skills would share any ByForm entry: key it BySkill. OVERDRAW adds a third pass and ECHO doubles it; all hits are on one ms. The clock is independent of the beat, so the telegraph must point at the next BEAT after ReadyAt, not ReadyAt itself.
- **showcase_needs**: RH_SHOT_HUNTER=metronome. A wave with 3+ creatures (base: three targets), and one with 1-2 creatures (SPARE SHOT doubling). Pose: the release, mid-flight, the contact on the beat, plus the last second of the clock. RH_SHOT_VARIATION=sig_metronome_clockwork:ROLL+RIM SHOT+OFF BEAT (a kill mid-volley so a shot retargets) and :HELD NOTE+SPARE SHOT. Pose a wave start for FIRST BEAT's opening cast and a clock that carried over a wave boundary.

## sig_unbroken_hold_fast — HOLD FAST
- **owner**: unbroken (THE UNBROKEN, CharacterRoster.cs:116-117)
- **kind**: Field (passive slot), Style.Snare, SkillEffect.Heal, IntervalMs 2000, Targets 1, ClipKey/FxKey 'trap', ShieldPerPulse 0.025 (SkillCatalogue.cs:940-944)
- **gameplay**: Defensive and self-only. Each tick grants shield worth 2.5% of max health through GrantShield (SoloBattle.cs:2298-2311), clamped by the shield cap; at the cap nothing is added and no event fires (1191-1196). DEEP ROOTS also heals 1% (growing per pulse with TAPROOT, counted per slot per wave). The shield then absorbs bites (ShieldAbsorbed) and can break (ShieldBroken). Nothing travels and enemies are untouched. Player decision: a defensive wall built on its own rhythm. What the player must see: the wall being rebuilt on each tick (a plate laid on), how full it is, and (DEEP ROOTS) the heal.
- **trigger**: Its own clock every IntervalMs, in arrears, never takes an action (SoloBattle.cs:2253-2263); GROUNDWORK also grants at wave start (1204-1206)
- **targets**: The champion (self)
- **persistent**: Yes: the shield is champion state (Champion.CurrentShield), shown as a bar plus the held barrier.
- **movement**: None.
- **duration**: Instant grant every 2 s (4 s BREASTWORK, 2.5 s FOOTINGS); the shield persists until absorbed or the wave resets.
- **stacking**: Adds up to the shield cap; TAPROOT's heal grows +30% per pulse per wave.
- **events**: Aura(slot, 0, ms), then ShieldGained(0, added, ms) only when something was added; with DEEP ROOTS, Heal(0, landed, ms) when not blocked by the per-wave heal ceiling (SoloBattle.cs:2187). GROUNDWORK: ShieldGained at 0 ms on wave open. Later, from bites: ShieldAbsorbed / ShieldBroken.
- **variations**: BREASTWORK (Machine): 5.5% every 4 s (a heavier, rarer plate); +COURSED STONE x1.55, +FOOTINGS every 2.5 s, +GROUNDWORK the wave opens with a plate already on (ShieldGained at 0 ms, which the screen currently mutes). DEEP ROOTS (Spirit): 1.2% shield + 1% heal per pulse (a heal must read alongside); +WELLSPRING heal x1.8, +TAPROOT the heal grows each pulse (visible escalation), +HEARTWOOD shield 3%.
- **current_presentation**: As a plain field it is the HELD aura (FieldRoles.Choose): HoldAura holds VfxProfiles.FieldAura behind the champion with FxFor('trap') = fx_unbroken_trap_strip8_512 (a row of spikes/caltrops), tinted by its Source colour, with the brightness spike on the fixed 1 s clock (HuntScreen.cs:1515-1516, 7344-7390). On ShieldGained (HuntScreen.cs:2431-2446): '+N SHIELD' callout (not at 0 ms), the shield-bar rim flash, sfx_shield_gain 0.40, the one-shot VfxProfiles.ShieldGain fx on the champion; the barrier fx_shield is held while shield stands (HoldShieldBarrier, 7420-7431). On Heal: '+N' callout and HealColumn fx. The Aura tick itself has no picture or sound.
- **current_problems**: The held art is a SPIKED SNARE row (a trap), standing behind the champion as an 'aura', which says 'trap', not 'wall/plate'. Its pulse runs on a 1 s clock, not the 2 s/4 s/2.5 s grant. The grant moment uses the generic shield feedback shared with every shield producer (the Machine set rung, BANKED and so on), so HOLD FAST's own act is not identifiable. At the cap the tick shows nothing (no event), so a capped wall reads as 'not working'. A '+N SHIELD' callout every 2 s may spam.
- **asset_quality**: fx_unbroken_trap_strip8_512: a row of grey spikes with a ring post that wobble/collapse and re-form: a trap object, mismatched with a shield. Listed as a DUAL held+fired strip in spec.json:159-160. Generic sfx_shield_gain/hit/break and fx_shield / fx_shield_break exist. The unbroken char strips include trap/cast/idle.
- **proposed_archetype**: FIELD-AURA (self-targeted, persistent defensive field ticking on its own clock)
- **secondary_archetype**: A new semantic case: SELF-BUFF / BARRIER BUILD (it shares the SHIELD/BARRIER presentation with other producers but needs its own per-tick 'plate laid' moment)
- **strategy**: A FieldRecipe keyed (unbroken, sig_unbroken_hold_fast) built from its Aura events and the ShieldGained/Heal at the same ms: on each tick, a short 'plate set' beat on the champion (stone/steel panels locking into the barrier, front-facing toward the enemies), contact on the tick ms, then the standing barrier. A distinct own tick cue (a stone/steel set). Replace the held fx_unbroken_trap aura entirely (wrong object). A capped tick should still show a quiet 'held/full' acknowledgement driven by the Aura event alone. DEEP ROOTS: roots/green glow joining the plate on the heal, its intensity from the pulse index (TAPROOT). GROUNDWORK: the plate already standing at wave open. Coordinate with the generic ShieldGained path so the recipe's tick replaces (not doubles) sfx_shield_gain and the ShieldGain one-shot at that ms, as IMPACT PRIORITY does for performed hits.
- **risk**: Shield events are shared with other producers on the same ms; the recipe must claim only the ShieldGained at its own tick ms. At-cap ticks produce no Core event other than Aura. Many shield one-shots plus callouts every 2 s is a noise risk; ADR-007/008 acknowledgement grammar applies. Unbroken can also weave a shared plain field, which then becomes the held aura.
- **showcase_needs**: RH_SHOT_HUNTER=unbroken. Pose: a tick with shield below the cap (the plate is laid), a tick at the cap (no ShieldGained), a bite absorbed, then a break. RH_SHOT_VARIATION=sig_unbroken_hold_fast:BREASTWORK+GROUNDWORK (the wave-open plate at 0 ms) and :DEEP ROOTS+TAPROOT (the heal escalating over several pulses). Enemies must bite enough to drain the shield so the rebuild is visible.

## sig_tower_slow_fall — SLOW FALL
- **owner**: tower (THE FALLING TOWER, CharacterRoster.cs:130-131; a second-tier Warden)
- **kind**: Field (passive slot), Style.Hammer, SkillEffect.Damage, IntervalMs 3000, Targets 1, BasePower 42 (per second), ClipKey/FxKey 'strike' (SkillCatalogue.cs:972-976)
- **gameplay**: A damage field that lands REAL hits on one creature: every 3 s a 'stone' hits the front enemy for BasePower x 3 s, instantly at the tick (LandSpread, countsAsActivation false, SoloBattle.cs:2465-2470). It is offensive, single-target (or 2-3 front), with no persistence; a death moves the next tick to the new front. Nothing travels in Core. The champion's MOMENTUM passive (first hit weaker, later stronger) applies to these hits. Player decision: free off-action damage on its own clock. What the player must perceive: a stone FALLING ONTO the front enemy, on time, every few seconds.
- **trigger**: Its own clock every IntervalMs, in arrears, never takes an action (SoloBattle.cs:2253-2263)
- **targets**: The first living enemy; ONE STONE: 2 (+1 CAPSTONE: 3) enemies in front (TargetsBonus, SoloBattle.cs:2468)
- **persistent**: No (the field is always on; no per-enemy state).
- **movement**: Core: none. Presentation: a vertical fall from above onto the target (it should arrive exactly on the tick ms, so the fall begins before it).
- **duration**: An instant hit every 3 s (1.5 COURSES, 1 DRYSTONE, 6 ONE STONE, 4 BEDDING IN).
- **stacking**: None.
- **events**: Aura(slot, 0, ms), then Strike(creature, dmg, Primary) on the front creature (1-3 with ONE STONE) at the same ms; EnemyDown on a kill. No Skill event.
- **variations**: COURSES (Body): every 1.5 s, half weight, ignores defence, +55% under 45% health (small rapid stones); +DRYSTONE every 1 s, +PLUMBLINE bonus 110%, +HAIRLINE threshold 70% (a finishing-hit accent is worth showing). ONE STONE (Nature): every 6 s on the 2 front enemies at x2.4 (one big stone spanning two); +CAPSTONE 3 enemies, +FULL COURSE x3.6, +BEDDING IN every 4 s.
- **current_presentation**: As a plain field it is the HELD aura: HoldAura holds VfxProfiles.FieldAura BEHIND THE CHAMPION with FxFor('strike') = fx_tower_strike_strip8_512 (a falling wedge stone with a dust burst), looped, tinted by Source, the brightness spike on the fixed 1 s clock (HuntScreen.cs:1515-1516, 7344-7390). The stone's blows are classified auraTick (HuntScreen.cs:2229-2260): no sound, no flash, no puff; one summed number over TargetSlot(). The enemy that is hit gets no visual at all.
- **current_problems**: The picture is inverted: the stone is drawn landing on the champion, looping forever, while the real hit on the enemy shows only a number. The pulse clock (1 s) is not the 3 s / 1.5 s / 1 s / 6 s / 4 s tick. No sound. ONE STONE's 2-3 targets are not shown. COURSES' finishing bonus and defence-ignore are invisible.
- **asset_quality**: fx_tower_strike_strip8_512: a grey wedge-shaped stone with a dust/splinter ring on frames 2-5, a clean single-impact read; usable as a reference or source for a falling-stone impact on the enemy, wrong as a held aura. A DUAL strip in spec.json (held+fired). Tower char strips exist (strike/cast/idle...). No sound.
- **proposed_archetype**: PROJECTILE (a vertical drop from above, contact on the tick) on its own clock, i.e. FIELD-AURA timing + PROJECTILE contact
- **secondary_archetype**: A new semantic case: an OFF-ACTION ARRIVAL (a timed strike that does not use the champion's figure), similar to PRESS's front 'arriving on the tick'
- **strategy**: A FieldRecipe-like performance keyed (tower, sig_tower_slow_fall), built per wave from its Aura events and the Strike targets at that ms (as PRESS takes the target from Break): a stone appears above the target ~300-500 ms ahead (a shadow on the ground growing as the telegraph), falls, and contacts on the tick ms with a directional impact, a buckle/flash on the creature, dust, and its own impact cue; the number after contact. No champion clip (it never takes the action), possibly a small glance or gesture. Scale and count by variation: COURSES small fast pebbles, ONE STONE one large block across 2-3 targets. Drop the held fx_tower_strike aura. Kills on the tick must be deferred to the contact as JAWS defers them, if the fall starts before the tick.
- **risk**: The fall must start BEFORE the tick (a pre-read of the wave's events, as FieldPerformance does), and the target can die to another blow between the telegraph and the tick, so the target must be resolved from the tick's own Strike. At DRYSTONE (1 s) stones overlap with every action; a quiet mode is needed. The tower may also weave a shared plain field, which becomes the held aura.
- **showcase_needs**: RH_SHOT_HUNTER=tower. A wave with 2+ creatures so a kill on the tick moves the front. Pose: the telegraph (stone above, shadow), the contact frame on the tick ms, and a tick that kills. RH_SHOT_VARIATION=sig_tower_slow_fall:COURSES+DRYSTONE (rapid 1 s stones, with the finishing bonus under 45%) and :ONE STONE+CAPSTONE+FULL COURSE (one heavy stone on three). A tick coinciding with a cast or swing (quiet mode).

## sig_quiver_backdraw — BACKDRAW
- **owner**: quiver
- **kind**: SkillKind.Reaction, Effect Damage, Style.Volley, On=ReactionOn.Kill, Beats 0, Interval 0, RearmMs 2500, Targets 2, BasePower 175, ClipKey/FxKey 'projectile' (SkillCatalogue.cs:1005-1009)
- **gameplay**: Instant, off-beat, takes no action. The moment an enemy dies, a two-arrow volley lands on the survivors in the same millisecond as the kill (no flight time in Core). Damage = PoweredBase*vow*first/later-cast mult*DamageMultiplier, lands as HitSource.Primary (can crit, carry, kill). A kill by the volley does not chain (answeringAKill). Nature source heals a sliver. Offensive. Physically: arrows from the Quiver to the remaining enemies. Player decision: a kill-chaining build (LOOSE AGAIN innate: every death also clears all other skill cooldowns, SoloBattle.cs:1851-1870). Moment to perceive: 'that death SENT arrows' - cause (death) then effect (arrows land on others). Also fires on the wave's LAST kill: there is no alive>0 gate, so the Skill event is emitted and the arm spent with no Strikes (LandSpread finds nothing, SoloBattle.cs:1919-1923, 2025-2064).
- **trigger**: Any creature death from any source (cast, swing, carry, bleed, reflect), dispatched inside LandOn's death branch below the LOOSE AGAIN clear (SoloBattle.cs:1872-1932). Gated by its own ReadyAt arm; re-entry flag stops a volley's own kill re-firing it; arm re-stamped after the volley so LOOSE AGAIN can't wipe it (1924-1926).
- **targets**: Base: first 2 living creatures in row order (LandSpread, SoloBattle.cs:2061). CLEAN SWEEP: whole wave. ONE SHAFT: 1.
- **persistent**: No persistent state; only the rearm (rail readiness via ReactionArmed).
- **movement**: Arrows travel from the Quiver to each target; the death should visibly 'pull' the shot (e.g. death spark/backdraw line toward the bow, then release).
- **duration**: Core: instant (0 ms). Presentation needs a short death->volley phrase (~250-400 ms) that must not delay the fight's result; rearm 2.5 s (12 s ONE SHAFT, 7 s SECOND SHAFT).
- **stacking**: Does not stack; at most once per rearm; never chains on its own kills. LOOSE AGAIN interaction makes deaths frequent.
- **events**: At the killing hit's atMs, in order: Strike(killed slot) -> EnemyDown(slot) -> [ReactionArmed when it rearmed] -> Skill(slot k, amount 0) -> one Strike per arrow (HitSource.Primary, crit flag) -> possibly EnemyDown for arrow kills. BeginRearm reports ReactionArmed later (SoloBattle.cs:1912). Heal if Nature.
- **variations**: CLEAN SWEEP (Mind): every living enemy, x0.65, +85% under 50% hp (WIDE SWEEP 75%, BARBED SWEEP +160%, SECOND STRING 2 hits/target x0.42) -> fan of arrows to whole row, execute-tinted hits on low-hp targets, double arrows. ONE SHAFT (Shadow): 1 arrow x6.5, rearm 12 s (HEAVY SHAFT x10, SECOND SHAFT 7 s, BROADHEAD 25% bleed) -> single heavy shaft, bigger weight, bleed after (SkillCatalogue.cs:1012-1030).
- **current_presentation**: No recipe (ActionRecipes/ReactionRecipes keyed only to seeker; ReactionRecipe.cs:459-470, ActionRecipe.cs:505-527). HuntScreen Skill case (HuntScreen.cs:2345-2391): style callout 'VOLLEY' (CalloutFor Style, 1197-1205, not the skill name), PlaySkillVfx -> VfxProfiles.ForSkill ClipKey 'projectile' -> CastProjectile (VfxProfiles.cs:137-139): ONE in-place spinning strip fx_quiver_projectile_strip8_512 (a white line-art arrow, Source-tinted additive) flown from the Quiver's chest to the FIRST struck creature only; sfx_cast 0.42 + sfx_hit 0.40 pitch +0.25 (the 'reaction' thud). Each arrow Strike: number captioned 'BACKDRAW' (trapName), generic flash, sfx_hit 0.22, ImpactWeak puff every other blow. Figure: reaction slots are excluded from beat clips; the 'trap' clip (char_quiver_trap = kneels and plants stakes) commits only when no further swing/cast exists in the wave (HuntScreen.cs:6453-6470) - effectively wave-tail only.
- **current_problems**: One flying arrow for a 2-arrow/whole-wave volley; the strip flies AFTER the damage already landed (Core has no travel, damage/flash/number at the kill ms, arrow arrives later); no visible cause->effect link from the dying creature; callout says VOLLEY not BACKDRAW; generic cast breath + pitched thud; if the trap clip plays at all it shows planting stakes, not shooting; phantom fire on the clearing kill plays callout/fx/sounds toward an empty row (castTarget null -> TargetSlot()); kill's own puff/death plume and the volley share one frame.
- **asset_quality**: fx_quiver_projectile: 8-frame white line-art arrow with speed streaks, 2026-09-01 generation, usable as a part reference only (ADR-010 says compose projectiles from parts, not strips). char_quiver_trap strip is the wrong action (stake planting). char_quiver_projectile strip (full bow draw and release) exists and is the right body action; no .clip.json timing for any Quiver strip. No BACKDRAW-specific sound.
- **proposed_archetype**: TARGET REACTION (kill-triggered) composed with PROJECTILE
- **secondary_archetype**: PROJECTILE (fan for CLEAN SWEEP, single heavy shaft for ONE SHAFT)
- **strategy**: A ReactionRecipe-style layer keyed (quiver, sig_quiver_backdraw) that never takes the figure's beat clip but may overlay a quick upper-body loose (or a release accent from the bow socket), plus composed arrows (ADR-010 parts) from the bow to each actual target. Because Core lands damage at the kill ms, use the JAWS pattern: defer the arrows' numbers/flash/deaths to presented arrival (ReactionEcho), keep the flight short (~150-220 ms). Suppress the phantom on zero-target volleys (presentation can skip a Skill with no following Strike; consider a Core alive>0 gate as a separate decision). Own release/contact cues; quiet ticks for fan widths.
- **risk**: Overlap with the killing action's own contact and death plume on the same frame; LOOSE AGAIN can make deaths cascade; deferring numbers vs the fight's already-resolved kills (use _deathDeferred pattern); phantom Skill on the last kill.
- **showcase_needs**: THE QUIVER with BACKDRAW slotted; a 3-5 creature wave so a kill leaves survivors; pose the first non-final kill (seek to the Skill event whose batch has Strikes). Variations: base (2 arrows), CLEAN SWEEP+SECOND STRING (whole row, double), ONE SHAFT (single heavy). Also pose the wave-clearing kill to prove no phantom. Show with a swing kill and with a cast kill (overlapping performances).

## sig_thornwall_narrows — NARROWS
- **owner**: thornwall
- **kind**: SkillKind.Reaction, Effect Damage, Style.Snare, On=ReactionOn.Bitten, RearmMs 2500, Targets 1, BasePower 170, Rules.PowerPerBiteAnswered 0.22, ClipKey/FxKey 'trap' (SkillCatalogue.cs:1034-1039)
- **gameplay**: Instant answer on the bite's millisecond, takes no action. It does NOT reflect the bite: a fixed blow (PoweredBase) x vow x first/later-cast x StylePower(Snare; Thornwall's REPRISAL x1.35) x (1 + 0.22 x answers already paid this wave) (SoloBattle.cs:3380-3398). Counter is per slot per wave, read before increment so first answer is the card number. Defensive-offensive (punishes being hit); grows the longer the wall stands. Moment to perceive: bitten -> the wall answers the front enemy, and the answer is visibly bigger each time this wave.
- **trigger**: An enemy bite (the wave's shared bite clock) while armed (SoloBattle.cs:3146-3173); not if the bite was pinned/intercepted.
- **targets**: Front creature (first alive). BRAMBLE: whole wave. SECOND STAKE: front two.
- **persistent**: Per-wave answer count (not emitted as an event; derivable by counting this slot's Skill events in the wave).
- **movement**: Thorns/stakes driven from the wall (Thornwall's shield/ground) to the front enemy; the 'narrowing' should close on the target.
- **duration**: Core instant; presentation ~400-600 ms answer phrase inside the bite; rearm 2.5 s (CHOKE 2 s, SNAPBACK x0.7).
- **stacking**: Growth stacks linearly per answered bite within a wave, resets per wave; BRAMBLE removes growth.
- **events**: At the bite ms: EnemyStrike(champion, healthLost) [+ShieldAbsorbed etc.] -> Skill(slot) -> Strike(front slot, HitSource.Primary) -> possibly EnemyDown. ReactionArmed when rearmed.
- **variations**: BRAMBLE (Nature): whole wave, base 110, no growth, +80% under 50% (UNDERGROWTH 75%, BLACK THORN +150%, BRIAR 170) -> all-row thorns, execute-tinted. CHOKE (Machine): rearm 2 s, growth 34% (DEEP THORN 55%, SNAPBACK faster, SECOND STAKE 2 targets) -> faster, visibly escalating single answer (SkillCatalogue.cs:1043-1062).
- **current_presentation**: No recipe. Skill case (HuntScreen.cs:2345-2391): callout 'SNARE', PlaySkillVfx ClipKey 'trap' -> CastTrap (VfxProfiles.cs:162-164): fx_thornwall_trap_strip8_512 (white barbed-wire ring, tinted) drawn GroundUnder at 0.80 of the WHOLE enemy row's width; sfx_cast 0.42 + sfx_hit 0.40 pitch +0.25; the Strike: number captioned 'NARROWS', generic flash, sfx_hit 0.22, puff every other blow. Figure: 'trap' clip (char_thornwall_trap: shield raise and brace) only when nothing else is due in the wave (HuntScreen.cs:6453-6470) - practically never mid-wave.
- **current_problems**: Single-target answer drawn as a row-wide ground burst; nothing connects the bite to the answer; growth (+22% per answer) is invisible except number size; the bite's own presentation (lunge/thud/number) and the answer land on the same frame with generic sounds; champion figure doesn't react; callout is the style word.
- **asset_quality**: fx_thornwall_trap: 8-frame white line-art barbed-wire ring breathing, 2026-09-01; generic, flat, not a contact. char_thornwall_trap (shield brace) and char_thornwall_attack (shield bash with baked white flash) exist, no .clip.json. No NARROWS sound.
- **proposed_archetype**: TARGET REACTION (bite-triggered)
- **secondary_archetype**: growth read (stack-tier escalation of the same answer)
- **strategy**: ReactionRecipe keyed (thornwall, sig_thornwall_narrows) on its own layer like JAWS: never takes the figure, answer composed at the front enemy (thorn stakes/brambles closing in from the ground at its feet), snap on a presented beat after the bite with number/death deferred (ReactionEcho). Escalation tiers from the per-wave answer index (count prior Skill events of the slot in the replay) scale the picture/sound. Variation BRAMBLE fans to each struck creature. Duck/yield rules against the bite and against JAWS-style reactions.
- **risk**: Same-frame clash with enemy bite lunge/thud and shield absorb; IRON-like stops don't apply here; growth tier must be read from presentation-side counting (no Core field) - keep it derived from events, not re-derived rules.
- **showcase_needs**: THE THORNWALL with NARROWS; a wave that bites repeatedly (Normal/Fast attack bias, enough health to survive 4+ bites); pose answer #1 and answer #4 of one wave to show growth; BRAMBLE on a 4-creature wave; CHOKE+SECOND STAKE (2 targets).

## sig_oathbound_oathmark — OATHMARK
- **owner**: oathbound
- **kind**: SkillKind.Reaction, Effect Amplify, Style.Sign, On=ReactionOn.Bitten, RearmMs 4000, Targets WholeWave, AmplifyPercent 0.90, AmplifyMs 3000, AmplifyWholeWave true, ClipKey/FxKey 'mark' (SkillCatalogue.cs:1066-1071)
- **gameplay**: Deals no damage. Opens (replaces) the champion's amplify window: all your damage +90% for 3 s (x1.8 with LINGER, x1.5 window and +25% power from TWICE SWORN innate). Sets ampBonus/ampWholeWave/ampFrontFull/ampUntil (SoloBattle.cs:3348-3374). SAID AGAIN deepens per opening up to a cap. Offensive buff paid for by being hit. Moment to perceive: bitten -> the mark opens on the enemies, and the next hits during the window are visibly amplified; then it closes.
- **trigger**: An enemy bite while armed (SoloBattle.cs:3146-3173, payout fork 3343-3375).
- **targets**: Base/OPEN WORD: amplify applies to the whole wave. SEALED WORD: front only (AmplifyWholeWave false, AmplifyFrontFull 2.6). FIRST WORD: front gets +220% while the rest keep the spread.
- **persistent**: Yes: an amplify window on the wave (or front enemy) for its duration; replaced by the next opening, not stacked.
- **movement**: None travels necessarily; an oath/chain sign binding each marked enemy, opened from the bite.
- **duration**: Window 3 s (SEALED WORD 1.8 s, LONG SEAL 3 s, OPEN WORD 2 s; x1.5 TWICE SWORN, x1.8 LINGER); rearm 4 s (2.5 s SHORTER OATH/OPEN WORD).
- **stacking**: No stacking; reopening refreshes; SAID AGAIN deepens +45% per reopening up to +180%; front enemy may hold a deeper mark (FIRST WORD/SEALED WORD).
- **events**: At the bite ms: EnemyStrike -> Marked(Slot = percent*100 rounded, Amount = window ms) -> Skill(slot). No Strike. ReactionArmed later. Note Marked's Slot carries the percent (WaveModel.cs:434 doc says the inverse).
- **variations**: SEALED WORD (Spirit): front only, +260% for 1.8 s (DEEP SEAL +390%, LONG SEAL 3 s, SHORTER OATH 2.5 s) -> one deep sigil on the front. OPEN WORD (Mind): whole wave +140% 2 s, rearm 2.5 s (WIDER WORD +230%, SAID AGAIN deepening, FIRST WORD front +220% with spread) -> wave-wide lighter marks with a front emphasis and a visible depth tier (SkillCatalogue.cs:1077-1095).
- **current_presentation**: No recipe (MarkRecipes keyed only seeker/sign_brand, MarkRecipe.cs:385-396). Skill case: callout 'SIGN', PlaySkillVfx ClipKey 'mark' -> CastMark (VfxProfiles.cs:167-169): fx_oathbound_mark_strip8_512 (white chain ring, tinted) overhead on ONE creature (castTarget null -> TargetSlot(), the front), sfx_cast + sfx_hit 0.40 pitch +0.25 (a damage thud for a no-damage effect, HuntScreen.cs:2387). The window is otherwise only visible as a hover-inspector status line 'MARKED YOUR SKILLS +N% FOR Xs' (HuntScreen.cs:2785, WaveReplay.cs:160-161,519-521). Amplified hits look like ordinary hits. Figure: 'trap' clip (char_oathbound_trap: lowers chains) only at wave tail.
- **current_problems**: Whole-wave mark shown on one enemy; no persistent window state on the arena; no closing; no link between amplified hits and the mark; damage thud on a buff; generic callout; front-only vs whole-wave variations indistinguishable.
- **asset_quality**: fx_oathbound_mark: 8-frame white chain-link ring, 2026-09-01, generic one-shot. char_oathbound_mark strip (hand raised) and char_oathbound_attack (chain whip) exist, no .clip.json. No OATHMARK sound.
- **proposed_archetype**: TARGET AFFLICTION (timed mark on targets) composed with TARGET REACTION (bite-triggered opening)
- **secondary_archetype**: amplified-hit accent (consumer hits during window)
- **strategy**: A MarkRecipe/MarkPerformance-style layer keyed (oathbound, sig_oathbound_oathmark), but window-driven from the Marked event (AtMs + Amount, percent in Slot) rather than BRAND's field ticks: open on the bite (reaction beat, no figure clip), hold a quiet persistent sign on each covered enemy (all or front-only per Def.AmplifyWholeWave/Rule.AmplifyFrontFull), accent hits landing inside the window, a readable close/fade. Drop the damage thud; own open cue, quiet hit accent. Must coexist with BRAND (Seeker-only today) and other Sign marks.
- **risk**: Window must be read from the event, not re-derived (LINGER, TWICE SWORN, MIND stretch extend ampUntil without a new Marked event - SoloBattle.cs:2980-2985 - so the presented close can be early); interaction with other amplify producers sharing ampUntil; clutter on 5-creature waves.
- **showcase_needs**: THE OATHBOUND with OATHMARK plus a damage skill (to show amplified hits in the window); a biting wave; pose the opening frame, mid-window with a hit, and the close; SEALED WORD (front only) vs OPEN WORD+FIRST WORD (whole wave with deeper front); SAID AGAIN at cap.

## sig_magpie_paying_work — PAYING WORK
- **owner**: magpie
- **kind**: SkillKind.Active, Effect Heal, Style.Drain, Beats 5, Targets 1, BasePower 250, Lifesteal 0.12, Rules.BossPower 1.60, ClipKey/FxKey 'transformation' (SkillCatalogue.cs:1099-1104)
- **gameplay**: An action: one heavy blow on the front enemy (x2.6 vs a boss via Amp, SoloBattle.cs:1518), then heals 12% of total dealt (SIPHON sharpens) via Heal (SoloBattle.cs:2937-2943). Offensive with sustain; a boss-killer signature. What physically happens: the Magpie robs/strikes the target and takes something back. Moment: the heavy hit on the target, then health flowing back to the Magpie; on a boss, a visibly bigger blow.
- **trigger**: Takes the champion's action every 5 beats (cast path; Skill event SoloBattle.cs:2641).
- **targets**: Front creature. LIGHT FINGERS: whole wave.
- **persistent**: None.
- **movement**: Melee lunge to the front enemy (or a thrown grab) and a return carrying the loot/health back to the Magpie.
- **duration**: One action per 5 beats (6 with LONG JOB); action ~ one beat (~0.7-1 s) plus heal return ~300-500 ms.
- **stacking**: No stacking; ECHO lands twice in one event; SECOND HELPING 2 hits/target.
- **events**: On the beat: Skill(slot) -> Strike(front, Primary, crit flag) [x2 with ECHO at same ms] -> EnemyDown? -> Heal(0, landed) (Heal emitted by Heal(), SoloBattle.cs:2119-2187).
- **variations**: STRIPPED BARE (Shadow): no lifesteal, boss +340% (CLEANED OUT +520%, PRISED OPEN ignores defence, LONG JOB 6 beats x1.45) -> no heal return, armour-pierce read, bigger boss blow. LIGHT FINGERS (Nature): whole wave x0.35, +80% under 50% (MANY POCKETS 72%, SECOND HELPING 2 hits x0.22, FULL HANDS 38% lifesteal) -> quick multi-target pickpocket touches, bigger heal (SkillCatalogue.cs:1108-1127).
- **current_presentation**: No ActionRecipe (no ByForm for magpie). Plain 'transformation' clip char_magpie_transformation_strip8_512 (arms spread, self pose) committed at 0.90 of beat, contact at 5/8 (HuntScreen.cs:6497-6506). PlaySkillVfx -> CastTransformation (VfxProfiles.cs:171-172): fx_magpie_transformation (white diamond/gem shower) on the CHAMPION at 1.05 x height, not on the target; callout 'DRAIN'; sfx_cast; Strike: plain number, generic flash, sfx_hit 0.22, puff every other. Heal: callout '+N' (green) and fx_heal column on the champion (HuntScreen.cs:2393-2401).
- **current_problems**: The blow has no contact on the target (effect plays on the Magpie); the champion pose is a self-buff, not a heavy strike; boss bonus invisible; lifesteal not linked to the target (column just appears); same as any transformation skill.
- **asset_quality**: fx_magpie_transformation: 8-frame white gem cluster, 2026-09-01, reads as loot sparkle, not a blow. char_magpie_transformation is a self pose. char_magpie_attack (green slash arc baked in) is closer to a strike. No .clip.json, no PAYING WORK sounds.
- **proposed_archetype**: MELEE CONTACT (Signature weight) composed with a heal return (drain stream back to the champion)
- **secondary_archetype**: LIGHT FINGERS: multi-target contact (dash-through or flurry); boss variant: Major weight accent
- **strategy**: MeleeActionRecipe in BySkill (magpie, sig_magpie_paying_work) with its own authored strip and .clip.json (commit/contact/recovery markers, socket), lunge to the front enemy, contact impact on the beat; then a drain/loot stream from the target to the Magpie timed to the Heal event (replace the generic heal column for this skill). Boss check from presentation state to scale weight. LIGHT FINGERS needs a multi-target variant (fan of quick touches or dash-through).
- **risk**: Heal ceiling/BLOOD MAGIC/NoHealing make Heal absent - picture must not promise it; Nature Signature leech also heals; LIGHT FINGERS lunge to many targets; handoff with swings at fast TEMPO.
- **showcase_needs**: THE MAGPIE with PAYING WORK; ordinary wave (single target), a boss wave (BossPower), STRIPPED BARE (no heal), LIGHT FINGERS on 4-5 creatures; pose at the Skill beat; hunter not at full health so Heal is non-zero.

## form:seeker — Basic attack (THE SEEKER)
- **owner**: seeker
- **kind**: Basic attack (MIGHT swing), not a SkillDef; HitSource.Swing
- **gameplay**: Damage = AutoAttackDamage x MIGHT x shape.AutoAttackDamage x IMPACT x (1+HARD HANDS swingPower), may ignore armour (TRAIL/rules), may lifesteal. Same for every champion; only presentation differs.
- **trigger**: Every beat on which no skill acted (SoloBattle.cs:3002-3027).
- **targets**: Front creature only (LandSpread targets 1).
- **persistent**: None.
- **movement**: Short melee lunge toward the front enemy (his strip is a step-in slash).
- **duration**: One beat (TEMPO-scaled; clip 0.55 of beat).
- **stacking**: N/A
- **events**: Strike(front slot, HitSource.Swing) on the beat; EnemyDown on kill; Heal if swing lifesteal; Beat event.
- **variations**: IMPACT swing (BODY 5p), TRAIL armour-ignore, HARD HANDS swing power - weight accents only.
- **current_presentation**: NOT the SPRAY path: SPRAY is ByForm (seeker,'projectile') for Projectile-Form skills (ActionRecipe.cs:510-513). The swing is a PLAIN clip 'attack' (char_seeker_attack: sword lunge slash) at 0.55 of the beat with contact at 5/8 (HuntScreen.cs:6497-6506, 6542-6544); on the Strike a 40 px push decays after contact (_champLunge, HuntScreen.cs:480, 2242, 3608); sfx_hit 0.38; generic flash (UsualFlash); ImpactWeak puff (fx_weakhit) every other blow; plain number.
- **current_problems**: No authored timing/socket, contact not frame-aligned to the blade, no contact impact or own sound; lunge happens after the hit; same generic thud for all ten champions.
- **asset_quality**: char_seeker_attack: good 8-frame sword lunge slash; no .clip.json.
- **proposed_archetype**: MELEE CONTACT (Ordinary weight)
- **strategy**: MeleeActionRecipe (Ordinary weight, lighter than HARD HANDS) for the 'attack' clip with a .clip.json, small/no root travel or short lunge, sword-slash contact and its own quiet cue; needs a lookup tier for the basic attack (ActionRecipes currently only resolves Skill casts).
- **risk**: Runs every beat: must stay quieter than skills; handoff/reservation code paths assume plain swings.
- **showcase_needs**: Seeker, no skills or only reactions so swings dominate; slow and capped TEMPO; a swing immediately before SPRAY/HARD HANDS (handoff).

## form:quiver — Basic attack (THE QUIVER)
- **owner**: quiver
- **kind**: Basic attack, HitSource.Swing
- **gameplay**: Identical Core swing (SoloBattle.cs:3002-3027).
- **trigger**: Every unclaimed beat.
- **targets**: Front creature.
- **persistent**: None.
- **movement**: Arrow from bow to front enemy; body stays put.
- **duration**: One beat.
- **stacking**: N/A
- **events**: Strike(Swing) on the beat.
- **variations**: none
- **current_presentation**: Plain 'attack' clip char_quiver_attack (draws and looses a bow), 40 px push after contact, sfx_hit 0.38, generic flash, puff every other blow. Nothing flies.
- **current_problems**: A bow shot with no arrow: the hit lands with no projectile, and the push lunges an archer forward.
- **asset_quality**: char_quiver_attack: decent bow draw/loose; no timing file; no arrow prop.
- **proposed_archetype**: PROJECTILE (Ordinary weight)
- **strategy**: ProjectileActionRecipe for the swing with an arrow prop (ADR-010 parts) released at a release marker so contact lands on the beat; no root lunge.
- **risk**: Travel time needs the clip started one flight early every beat at high TEMPO.
- **showcase_needs**: Quiver, swings only; fast TEMPO.

## form:thornwall — Basic attack (THE THORNWALL)
- **owner**: thornwall
- **kind**: Basic attack, HitSource.Swing
- **gameplay**: Identical Core swing.
- **trigger**: Every unclaimed beat.
- **targets**: Front creature.
- **persistent**: None.
- **movement**: Short shield-bash lunge.
- **duration**: One beat.
- **stacking**: N/A
- **events**: Strike(Swing).
- **variations**: none
- **current_presentation**: Plain 'attack' clip char_thornwall_attack (shield bash with a white flash baked into frames 3-5), push, sfx_hit, generic flash, puff.
- **current_problems**: Baked flash in the body strip; no contact at the enemy; no reach.
- **asset_quality**: char_thornwall_attack: heavy shield bash, baked VFX in frames (should be stripped).
- **proposed_archetype**: MELEE CONTACT (Ordinary)
- **strategy**: Melee recipe with lunge and shield contact; clean strip without baked flash.
- **risk**: Wide shield silhouette vs enemy row gap.
- **showcase_needs**: Thornwall swings only.

## form:oathbound — Basic attack (THE OATHBOUND)
- **owner**: oathbound
- **kind**: Basic attack, HitSource.Swing
- **gameplay**: Identical Core swing.
- **trigger**: Every unclaimed beat.
- **targets**: Front creature.
- **persistent**: None.
- **movement**: Chain extends to the front enemy (reach weapon) with body mostly in place.
- **duration**: One beat.
- **stacking**: N/A
- **events**: Strike(Swing).
- **variations**: none
- **current_presentation**: Plain 'attack' clip char_oathbound_attack (chain whip lash), push, sfx_hit, generic flash, puff.
- **current_problems**: Chain never reaches the enemy; no contact.
- **asset_quality**: char_oathbound_attack: chain lash, short reach.
- **proposed_archetype**: MELEE CONTACT (reach) - possibly PROJECTILE-like tether
- **strategy**: Composed chain/tether from the hand socket to the target with contact on the beat.
- **risk**: Thinnest silhouette (162 px) - contact sizing.
- **showcase_needs**: Oathbound swings only.

## form:magpie — Basic attack (THE MAGPIE)
- **owner**: magpie
- **kind**: Basic attack, HitSource.Swing
- **gameplay**: Identical Core swing.
- **trigger**: Every unclaimed beat.
- **targets**: Front creature.
- **persistent**: None.
- **movement**: Short dash-slash.
- **duration**: One beat.
- **stacking**: N/A
- **events**: Strike(Swing).
- **variations**: none
- **current_presentation**: Plain 'attack' clip char_magpie_attack (dagger slash with a baked green arc), push, sfx_hit, generic flash, puff.
- **current_problems**: Baked green slash in the strip ignores Source tint; no contact at the enemy.
- **asset_quality**: char_magpie_attack: lively, baked effect.
- **proposed_archetype**: MELEE CONTACT (Ordinary)
- **strategy**: Melee recipe; strip without baked arc or keep it as an authored smear.
- **risk**: Must stay distinct from PAYING WORK's heavy blow.
- **showcase_needs**: Magpie swings only.

## form:anvil — Basic attack (THE ANVIL)
- **owner**: anvil
- **kind**: Basic attack, HitSource.Swing
- **gameplay**: Identical Core swing; DEADWEIGHT innate stores 33% of a hit in a surviving target (Swing included as generator).
- **trigger**: Every unclaimed beat.
- **targets**: Front creature.
- **persistent**: None (see DEADWEIGHT row).
- **movement**: Lunge punch.
- **duration**: One beat.
- **stacking**: N/A
- **events**: Strike(Swing); DeadweightStored/Released.
- **variations**: none
- **current_presentation**: Plain 'attack' clip char_anvil_attack (punch), push, sfx_hit, generic flash, puff.
- **current_problems**: No contact; generic.
- **asset_quality**: char_anvil_attack: punch, decent.
- **proposed_archetype**: MELEE CONTACT (Ordinary)
- **strategy**: Melee recipe, fist socket.
- **risk**: Low.
- **showcase_needs**: Anvil swings.

## form:chorus — Basic attack (THE CHORUS)
- **owner**: chorus
- **kind**: Basic attack, HitSource.Swing
- **gameplay**: Identical Core swing.
- **trigger**: Every unclaimed beat.
- **targets**: Front creature.
- **persistent**: None.
- **movement**: Charms fly to the front enemy.
- **duration**: One beat.
- **stacking**: N/A
- **events**: Strike(Swing).
- **variations**: none
- **current_presentation**: Plain 'attack' clip char_chorus_attack (throws bone charms; small blue glow baked), push, sfx_hit, generic flash, puff.
- **current_problems**: Thrown charms never reach the enemy; lunge on a ranged throw.
- **asset_quality**: char_chorus_attack: throw with baked bone props and glow.
- **proposed_archetype**: PROJECTILE (Ordinary)
- **strategy**: Projectile recipe with a charm prop; remove baked props from late frames.
- **risk**: Flight timing at high TEMPO.
- **showcase_needs**: Chorus swings.

## form:metronome — Basic attack (THE METRONOME)
- **owner**: metronome
- **kind**: Basic attack, HitSource.Swing
- **gameplay**: Identical Core swing; FIRST BEAT doubles the first hit on each enemy.
- **trigger**: Every unclaimed beat.
- **targets**: Front creature.
- **persistent**: None.
- **movement**: Lunge punch.
- **duration**: One beat.
- **stacking**: N/A
- **events**: Strike(Swing).
- **variations**: none
- **current_presentation**: Plain 'attack' clip char_metronome_attack (running punch), push, sfx_hit, generic flash, puff.
- **current_problems**: No contact; first-hit doubling not shown.
- **asset_quality**: char_metronome_attack: lunge punch.
- **proposed_archetype**: MELEE CONTACT (Ordinary)
- **strategy**: Melee recipe.
- **risk**: Low.
- **showcase_needs**: Metronome swings.

## form:unbroken — Basic attack (THE UNBROKEN)
- **owner**: unbroken
- **kind**: Basic attack, HitSource.Swing
- **gameplay**: Identical Core swing.
- **trigger**: Every unclaimed beat.
- **targets**: Front creature.
- **persistent**: None.
- **movement**: Unclear (bolt from the hand?).
- **duration**: One beat.
- **stacking**: N/A
- **events**: Strike(Swing).
- **variations**: none
- **current_presentation**: Plain 'attack' clip char_unbroken_attack (chest glows orange, a spark leaves the hand - baked), push, sfx_hit, generic flash, puff.
- **current_problems**: Reads as a cast with a baked glow; nothing reaches the enemy.
- **asset_quality**: char_unbroken_attack: baked orange VFX in frames.
- **proposed_archetype**: PROJECTILE or MELEE CONTACT - needs an owner decision on the action
- **strategy**: Decide the action first; then recipe.
- **risk**: Ambiguous action.
- **showcase_needs**: Unbroken swings.

## form:tower — Basic attack (THE FALLING TOWER)
- **owner**: tower
- **kind**: Basic attack, HitSource.Swing
- **gameplay**: Identical Core swing; MOMENTUM: first hit x0.85, later x1.35.
- **trigger**: Every unclaimed beat.
- **targets**: Front creature.
- **persistent**: None.
- **movement**: Lunge with the hammer, or a ground shock travelling to the target.
- **duration**: One beat.
- **stacking**: N/A
- **events**: Strike(Swing).
- **variations**: none
- **current_presentation**: Plain 'attack' clip char_tower_attack (overhead hammer slam into the ground at his own feet), push, sfx_hit, generic flash, puff.
- **current_problems**: The hammer lands at his feet, not on the enemy.
- **asset_quality**: char_tower_attack: hammer slam in place.
- **proposed_archetype**: MELEE CONTACT (Ordinary)
- **strategy**: Melee recipe with lunge (the slam must land on the creature).
- **risk**: Low.
- **showcase_needs**: Tower swings.

## proc:heal — Heal (lifesteal / Nature leech / heal-on-bite / pulses)
- **owner**: shared
- **kind**: BattleEventKind.Heal from Heal() (SoloBattle.cs:2119-2187)
- **gameplay**: Restores health, under the per-wave ceiling; blocked by NoHealing (BLOOD MAGIC, JUGGERNAUT).
- **trigger**: Any heal producer: skill Lifesteal, swing lifesteal, Nature signature leech, HealOnBite, DEEP ROOTS pulses, etc.
- **targets**: Champion
- **persistent**: No
- **movement**: Should flow from the source (target for drain) to the champion.
- **duration**: ~0.8 s
- **stacking**: Frequent with lifesteal; can spam.
- **events**: Heal(slot 0, landed amount).
- **variations**: n/a
- **current_presentation**: Callout '+N' green and fx_heal column (HealColumn, Standing anchor, 0.95, pinned) on the champion (HuntScreen.cs:2393-2401; VfxProfiles.cs:55-56). No sound.
- **current_problems**: One generic picture for every source; not linked to the cause (drain from target vs pulse); no sound.
- **asset_quality**: fx_heal strip (shared, Sep 2026 generation).
- **proposed_archetype**: composition: champion-side receive accent, owned by the producing skill when it has a recipe
- **strategy**: Keep a quiet generic receive; let recipes (PAYING WORK) claim their heal and suppress the generic column.
- **risk**: Spam on fast builds.
- **showcase_needs**: Hurt hunter with a lifesteal skill.

## proc:shield — Shield (gain / absorb / break / standing barrier)
- **owner**: shared
- **kind**: ShieldGained/ShieldAbsorbed/ShieldBroken events; Champion.CurrentShield state
- **gameplay**: Absorbs bite damage before health.
- **trigger**: GrantShield producers (PLATING, HOLD FAST, DEEP ROOTS, etc.), bites eating it.
- **targets**: Champion
- **persistent**: Yes
- **movement**: none
- **duration**: Held while > 0.
- **stacking**: Capped pool.
- **events**: ShieldGained(amount added), ShieldAbsorbed(amount), ShieldBroken (once on crossing) (SoloBattle.cs:1196, 3229, 3237).
- **variations**: n/a
- **current_presentation**: Held fx_shield barrier breathing (HoldShieldBarrier, HuntScreen.cs:7420-7430), gain flare + '+N SHIELD' + sfx_shield_gain, absorb flare + sfx_shield_hit, break 'SHIELD BROKEN' + sfx_shield_break + fx_shield_break (HuntScreen.cs:2431-2477; VfxProfiles.cs:87-110).
- **current_problems**: Complete and coherent but pre-ADR-011 (strip-based, not reviewed as a gold standard); same picture for every producer.
- **asset_quality**: fx_shield ring and fx_shield_break regenerated to fill the frame (2026-09-04); dedicated sounds exist.
- **proposed_archetype**: FIELD-AURA (self, persistent) + receive accents
- **strategy**: Likely keep; review against ADR-011 layering when a producer signature (HOLD FAST) gets its recipe.
- **risk**: Low.
- **showcase_needs**: RH_SHOT_SHIELDFX gain/absorb poses exist (HuntScreen.cs:2485-2501).

## proc:undying — UNDYING / SECOND WIND
- **owner**: keystone undying; unbroken innate
- **kind**: BuildTrigger.Undying; BattleEventKind.Undying (Amount = ms)
- **gameplay**: Leaves the champion on 1 health.
- **trigger**: Killing blow, once per expedition (SoloBattle.cs:3426-3439).
- **targets**: Champion
- **persistent**: brief
- **movement**: none
- **duration**: UndyingShieldMs
- **stacking**: once per run
- **events**: Undying(0, UndyingShieldMs).
- **variations**: n/a
- **current_presentation**: Callout 'UNDYING' gold + ShieldUndying (fx_shield tinted gold) (HuntScreen.cs:2402-2405). No sound.
- **current_problems**: Rare major event with no sound and a reused shield picture.
- **asset_quality**: reuses fx_shield.
- **proposed_archetype**: new semantic case: Major self-save (receive)
- **strategy**: Own cue and a refusal-of-death beat at Major weight.
- **risk**: Rare - needs a fixture.
- **showcase_needs**: Unbroken or keystone undying, lethal bite fixture.

## proc:deadweight — DEADWEIGHT (Anvil innate)
- **owner**: anvil
- **kind**: SkillShape.DeadweightShare 0.33; DeadweightStored/DeadweightReleased events
- **gameplay**: A third of the hit is stored in the survivor and lands with the next hit (HitSource.Deadweight strike).
- **trigger**: Primary/Swing hit leaving a target alive stores; next Primary/Swing on it releases.
- **targets**: Struck creature
- **persistent**: Yes, per creature
- **movement**: none
- **duration**: Until next hit or death
- **stacking**: replaced, not added
- **events**: DeadweightStored(slot, held), DeadweightReleased(slot, amount) + Strike(Deadweight).
- **variations**: n/a
- **current_presentation**: No handling in HuntScreen (no case for either kind); the release is a generic Strike (number, flash, puff).
- **current_problems**: Stored state invisible; release indistinguishable from any hit.
- **asset_quality**: none
- **proposed_archetype**: TARGET AFFLICTION (stored state) + release accent
- **strategy**: Quiet embedded-weight mark on the creature; release accent on the next contact.
- **risk**: Every hit generates - must be very quiet.
- **showcase_needs**: Anvil vs a tanky front creature.

## proc:weaver_echo — WEAVER echo cast
- **owner**: keystone weaver
- **kind**: BuildTrigger.Weaver
- **gameplay**: Next skill also lands at 45%.
- **trigger**: After every cast.
- **targets**: As the next skill in the build
- **persistent**: No
- **movement**: as woven skill
- **duration**: instant
- **stacking**: one per cast
- **events**: Second Skill(wovenIdx) at the same ms + its Strikes (SoloBattle.cs:2953-2973).
- **variations**: n/a
- **current_presentation**: Treated as a full plain cast: second callout, PlaySkillVfx, sfx_cast at the same frame; even if the woven skill has a recipe (SPRAY/HARD HANDS) it plays the plain path because the performance is keyed to the cast's slot (HuntScreen.cs:2366, 2380-2382).
- **current_problems**: Two callouts/two breaths on one frame; recipe skills lose their look when woven.
- **asset_quality**: n/a
- **proposed_archetype**: composition: echo of the woven skill's archetype at reduced weight
- **strategy**: An echo mode (quiet) for each recipe; dedupe callouts.
- **risk**: Every recipe needs a quiet echo variant.
- **showcase_needs**: Weaver with two damage skills.

## proc:bleed_reflect — Bleed / VENOM ticks and THORNS reflect
- **owner**: keystone venomancer; shared
- **kind**: HitSource.Bleed / HitSource.Reflect strikes
- **gameplay**: Derived damage.
- **trigger**: Poison pool ticks every 500 ms (SoloBattle.cs:2222); reflect on each bite (3306-3324).
- **targets**: Front (bleed); every biter (reflect)
- **persistent**: pool persists
- **movement**: none
- **duration**: continuous
- **stacking**: pool accumulates
- **events**: Strike with Hit Bleed/Reflect (FromSkill true).
- **variations**: n/a
- **current_presentation**: Generic: number, flash, sfx_hit 0.22, puff every other blow; no DOT state shown.
- **current_problems**: Poison pool invisible; reflect not tied to the bite.
- **asset_quality**: none
- **proposed_archetype**: TARGET AFFLICTION (bleed) / TARGET REACTION (reflect)
- **strategy**: Quiet standing affliction and a reflect spark on the biter; no flash strobe.
- **risk**: High frequency.
- **showcase_needs**: Venomancer build; THORNS build vs fast biters.

## proc:charge — CHARGE pool (REND / DYNAMO / CAPACITOR / LODESTONE)
- **owner**: keystones
- **kind**: BattleEventKind.Charge (Amount = pool after change)
- **gameplay**: Shared stack; REND dumps +5%/charge into a Strike.
- **trigger**: Casts store; DYNAMO bites store 2; REND strikes dump.
- **targets**: Champion
- **persistent**: Yes
- **movement**: none
- **duration**: per wave
- **stacking**: to cap
- **events**: Charge(0, pool).
- **variations**: n/a
- **current_presentation**: HUD chip 'CHARGE n/cap' only (HuntScreen.cs:2424-2425, 5180); no arena picture of storing or the dump.
- **current_problems**: The REND spike is unreadable in the arena.
- **asset_quality**: none
- **proposed_archetype**: FIELD-AURA (self state) + dump accent
- **strategy**: Small orbiting pips; dump accent on the REND strike.
- **risk**: Low.
- **showcase_needs**: REND build with a Strike skill.
